
using System;
using System.Collections.Generic;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    internal class SetorDAO
    {
        public List<Setor> Listar()
        {
            var setores = new List<Setor>();

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            const string sql = @"
                SELECT id_setor, nome, descricao
                FROM setor
                ORDER BY nome";

            using var comando = new MySqlCommand(sql, conexao);
            using var leitor = comando.ExecuteReader();

            while (leitor.Read())
            {
                setores.Add(new Setor
                {
                    IdSetor = leitor.GetInt32("id_setor"),
                    Nome = leitor.GetString("nome"),
                    Descricao = leitor.IsDBNull(
                        leitor.GetOrdinal("descricao"))
                        ? null
                        : leitor.GetString("descricao")
                });
            }

            return setores;
        }

        public int Cadastrar(Setor item)
        {
            Validar(item);
            const string sql = "INSERT INTO setor (nome, descricao) VALUES (@nome, @descricao)";
            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();
            using var comando = new MySqlCommand(sql, conexao);
            comando.Parameters.AddWithValue("@nome", item.Nome.Trim());
            comando.Parameters.AddWithValue("@descricao", string.IsNullOrWhiteSpace(item.Descricao) ? DBNull.Value : item.Descricao.Trim());
            comando.ExecuteNonQuery();
            return Convert.ToInt32(comando.LastInsertedId);
        }

        public void Atualizar(Setor item)
        {
            Validar(item);
            if (item.IdSetor <= 0)
                throw new ArgumentException("Selecione um registro válido.");
            const string sql = "UPDATE setor SET nome = @nome, descricao = @descricao WHERE id_setor = @id";
            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();
            using var comando = new MySqlCommand(sql, conexao);
            comando.Parameters.AddWithValue("@id", item.IdSetor);
            comando.Parameters.AddWithValue("@nome", item.Nome.Trim());
            comando.Parameters.AddWithValue("@descricao", string.IsNullOrWhiteSpace(item.Descricao) ? DBNull.Value : item.Descricao.Trim());
            comando.ExecuteNonQuery();
        }

        private static void Validar(Setor item)
        {
            if (string.IsNullOrWhiteSpace(item.Nome))
                throw new ArgumentException("O nome é obrigatório.");
            if (item.Nome.Trim().Length > 100)
                throw new ArgumentException("O nome deve ter no máximo 100 caracteres.");
            if (item.Descricao?.Length > 255)
                throw new ArgumentException("A descrição deve ter no máximo 255 caracteres.");
        }
    }
}
