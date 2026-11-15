
using System;
using System.Data;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    internal class MovimentacaoService
    {
        public void Registrar(Movimentacao mov)
        {
            mov.Validar();

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            using var transacao = conexao.BeginTransaction(
                IsolationLevel.ReadCommitted);

            try
            {
                ValidarUsuarioAtivo(conexao, transacao, mov.IdUsuario);

                int? idLote = mov.IdLote;

                // Bloqueia os vínculos envolvidos em ordem fixa,
                // reduzindo o risco de deadlocks.
                int origem = mov.IdProdutoSetor;
                int? destino = mov.IdProdutoSetorDestino;

                int primeiro = destino.HasValue
                    ? Math.Min(origem, destino.Value)
                    : origem;

                int segundo = destino.HasValue
                    ? Math.Max(origem, destino.Value)
                    : origem;

                int produtoPrimeiro = ObterProdutoComBloqueio(
                    conexao, transacao, primeiro);

                int produtoOrigem = produtoPrimeiro;

                if (primeiro != origem)
                {
                    produtoOrigem = ObterProdutoComBloqueio(
                        conexao, transacao, segundo);
                }
                else if (destino.HasValue)
                {
                    ObterProdutoComBloqueio(
                        conexao, transacao, segundo);
                }

                int? produtoLote = idLote.HasValue
                    ? ObterProdutoLote(conexao, transacao, idLote.Value)
                    : null;

                ValidarProdutoAtivo(conexao, transacao, produtoOrigem);

                if (produtoLote.HasValue && produtoOrigem != produtoLote.Value)
                    throw new InvalidOperationException(
                        "O lote não pertence ao produto informado.");

                if (destino.HasValue)
                {
                    int produtoDestino = ObterProduto(
                        conexao, transacao, destino.Value);

                    if (produtoDestino != produtoOrigem)
                        throw new InvalidOperationException(
                            "A transferência deve ocorrer entre setores do mesmo produto.");
                }

                // Impede movimentações sobre saldos já divergentes: a soma dos
                // lotes de um setor deve corresponder ao saldo consolidado.
                ValidarConsistenciaSaldo(conexao, transacao, origem);
                if (destino.HasValue)
                    ValidarConsistenciaSaldo(conexao, transacao, destino.Value);

                switch (mov.Tipo)
                {
                    case TipoMovimentacao.ENTRADA:
                        AlterarSaldo(
                            conexao, transacao,
                            origem, idLote, mov.Quantidade);
                        break;

                    case TipoMovimentacao.SAIDA:
                        AlterarSaldo(
                            conexao, transacao,
                            origem, idLote, -mov.Quantidade);
                        break;

                    case TipoMovimentacao.TRANSFERENCIA:
                        AlterarSaldo(
                            conexao, transacao,
                            origem, idLote, -mov.Quantidade);

                        AlterarSaldo(
                            conexao, transacao,
                            destino!.Value, idLote, mov.Quantidade);
                        break;

                    default:
                        throw new ArgumentException(
                            "Tipo de movimentação inválido.");
                }

                // Validação dentro da mesma transação, antes de gravar o histórico.
                ValidarConsistenciaSaldo(conexao, transacao, origem);
                if (destino.HasValue)
                    ValidarConsistenciaSaldo(conexao, transacao, destino.Value);

                RegistrarHistorico(conexao, transacao, mov);

                transacao.Commit();
            }
            catch
            {
                transacao.Rollback();
                throw;
            }
        }

        private static void ValidarConsistenciaSaldo(
            MySqlConnection conexao, MySqlTransaction transacao, int idProdutoSetor)
        {
            const string sql = @"SELECT ps.quantidade_disponivel,
                COALESCE(SUM(els.quantidade_disponivel), 0) + ps.quantidade_sem_lote AS total_lotes
                FROM produto_setor ps
                LEFT JOIN estoque_lote_setor els
                    ON els.id_produto_setor = ps.id_produto_setor
                WHERE ps.id_produto_setor = @vinculo
                GROUP BY ps.id_produto_setor, ps.quantidade_disponivel, ps.quantidade_sem_lote";

            using var cmd = new MySqlCommand(sql, conexao, transacao);
            cmd.Parameters.AddWithValue("@vinculo", idProdutoSetor);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                throw new InvalidOperationException("Vínculo produto-setor não encontrado.");
            decimal saldoSetor = reader.GetDecimal(0);
            decimal somaLotes = reader.GetDecimal(1);
            if (saldoSetor != somaLotes)
                throw new InvalidOperationException(
                    $"Estoque inconsistente no vínculo {idProdutoSetor}: " +
                    $"saldo do setor {saldoSetor:N2}, soma dos lotes {somaLotes:N2}. " +
                    "Regularize o estoque antes de registrar movimentações.");
        }

        private static void ValidarUsuarioAtivo(
            MySqlConnection conexao, MySqlTransaction transacao, int idUsuario)
        {
            using var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM usuario WHERE id_usuario = @id AND ativo = 1",
                conexao, transacao);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                throw new InvalidOperationException("O usuário responsável não está ativo.");
        }

        private static void ValidarProdutoAtivo(
            MySqlConnection conexao, MySqlTransaction transacao, int idProduto)
        {
            using var cmd = new MySqlCommand(
                "SELECT COUNT(*) FROM produto WHERE id_produto = @id AND situacao = 1",
                conexao, transacao);
            cmd.Parameters.AddWithValue("@id", idProduto);
            if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
                throw new InvalidOperationException("Não é permitido movimentar produto inativo.");
        }

        private static int ObterProdutoComBloqueio(
            MySqlConnection conexao,
            MySqlTransaction transacao,
            int idProdutoSetor)
        {
            const string sql = @"
                SELECT id_produto
                FROM produto_setor
                WHERE id_produto_setor = @id
                FOR UPDATE";

            using var comando = new MySqlCommand(
                sql, conexao, transacao);

            comando.Parameters.AddWithValue(
                "@id", idProdutoSetor);

            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
                throw new InvalidOperationException(
                    "Vínculo produto-setor não encontrado.");

            return Convert.ToInt32(resultado);
        }

        private static int ObterProduto(
            MySqlConnection conexao,
            MySqlTransaction transacao,
            int idProdutoSetor)
        {
            const string sql = @"
                SELECT id_produto
                FROM produto_setor
                WHERE id_produto_setor = @id";

            using var comando = new MySqlCommand(
                sql, conexao, transacao);

            comando.Parameters.AddWithValue(
                "@id", idProdutoSetor);

            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
                throw new InvalidOperationException(
                    "Setor de destino não encontrado.");

            return Convert.ToInt32(resultado);
        }

        private static int ObterProdutoLote(
            MySqlConnection conexao,
            MySqlTransaction transacao,
            int idLote)
        {
            const string sql = @"
                SELECT id_produto
                FROM lote_produto
                WHERE id_lote = @id";

            using var comando = new MySqlCommand(
                sql, conexao, transacao);

            comando.Parameters.AddWithValue("@id", idLote);

            object? resultado = comando.ExecuteScalar();

            if (resultado == null)
                throw new InvalidOperationException(
                    "Lote não encontrado.");

            return Convert.ToInt32(resultado);
        }

        private static void AlterarSaldo(
            MySqlConnection conexao,
            MySqlTransaction transacao,
            int idProdutoSetor,
            int? idLote,
            decimal variacao)
        {
            if (!idLote.HasValue)
            {
                const string sqlSemLote = @"
                    UPDATE produto_setor
                    SET quantidade_sem_lote = quantidade_sem_lote + @variacao,
                        quantidade_disponivel = quantidade_disponivel + @variacao
                    WHERE id_produto_setor = @vinculo
                      AND quantidade_sem_lote + @variacao >= 0
                      AND quantidade_disponivel + @variacao >= 0";
                using var semLote = new MySqlCommand(sqlSemLote, conexao, transacao);
                semLote.Parameters.AddWithValue("@variacao", variacao);
                semLote.Parameters.AddWithValue("@vinculo", idProdutoSetor);
                if (semLote.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException("Saldo sem lote insuficiente ou setor inválido.");
                return;
            }

            if (variacao > 0)
            {
                const string sqlEntrada = @"
                    INSERT INTO estoque_lote_setor
                        (id_produto_setor, id_lote,
                         quantidade_disponivel)
                    VALUES
                        (@vinculo, @lote, @quantidade)
                    ON DUPLICATE KEY UPDATE
                        quantidade_disponivel =
                            quantidade_disponivel + @quantidade";

                using var comando = new MySqlCommand(
                    sqlEntrada, conexao, transacao);

                comando.Parameters.AddWithValue(
                    "@vinculo", idProdutoSetor);
                comando.Parameters.AddWithValue(
                    "@lote", idLote.Value);
                comando.Parameters.AddWithValue(
                    "@quantidade", variacao);

                comando.ExecuteNonQuery();
            }
            else
            {
                const string sqlSaida = @"
                    UPDATE estoque_lote_setor
                    SET quantidade_disponivel =
                        quantidade_disponivel - @quantidade
                    WHERE id_produto_setor = @vinculo
                      AND id_lote = @lote
                      AND quantidade_disponivel >= @quantidade";

                using var comando = new MySqlCommand(
                    sqlSaida, conexao, transacao);

                comando.Parameters.AddWithValue(
                    "@vinculo", idProdutoSetor);
                comando.Parameters.AddWithValue(
                    "@lote", idLote.Value);
                comando.Parameters.AddWithValue(
                    "@quantidade", -variacao);

                if (comando.ExecuteNonQuery() != 1)
                    throw new InvalidOperationException(
                        "Quantidade insuficiente no lote selecionado.");
            }

            const string sqlSetor = @"
                UPDATE produto_setor
                SET quantidade_disponivel =
                    quantidade_disponivel + @variacao
                WHERE id_produto_setor = @vinculo
                  AND quantidade_disponivel + @variacao >= 0";

            using var atualizar = new MySqlCommand(
                sqlSetor, conexao, transacao);

            atualizar.Parameters.AddWithValue(
                "@variacao", variacao);
            atualizar.Parameters.AddWithValue(
                "@vinculo", idProdutoSetor);

            if (atualizar.ExecuteNonQuery() != 1)
                throw new InvalidOperationException(
                    "Saldo insuficiente ou vínculo inválido.");
        }

        private static void RegistrarHistorico(
            MySqlConnection conexao,
            MySqlTransaction transacao,
            Movimentacao mov)
        {
            const string sql = @"
                INSERT INTO movimentacao
                    (tipo, quantidade,
                     id_produto_setor,
                     id_produto_setor_destino,
                     id_lote, id_usuario)
                VALUES
                    (@tipo, @quantidade,
                     @origem, @destino,
                     @lote, @usuario)";

            using var comando = new MySqlCommand(
                sql, conexao, transacao);

            comando.Parameters.AddWithValue(
                "@tipo", mov.Tipo.ToString());
            comando.Parameters.AddWithValue(
                "@quantidade", mov.Quantidade);
            comando.Parameters.AddWithValue(
                "@origem", mov.IdProdutoSetor);
            comando.Parameters.AddWithValue(
                "@destino",
                (object?)mov.IdProdutoSetorDestino
                ?? DBNull.Value);
            comando.Parameters.AddWithValue(
                "@lote", (object?)mov.IdLote ?? DBNull.Value);
            comando.Parameters.AddWithValue(
                "@usuario", mov.IdUsuario);

            comando.ExecuteNonQuery();

            mov.IdMovimentacao =
                Convert.ToInt32(comando.LastInsertedId);
        }
    }
}
