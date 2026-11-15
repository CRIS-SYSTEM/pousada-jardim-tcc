
using System;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    internal class UsuarioDAO
    {
        // Consulta um usuário pelo login e verifica a senha.
        public Usuario? Autenticar(string login, string senha)
        {
            const string sql = @"
                SELECT id_usuario, nome, login,
                       senha_hash, perfil, ativo
                FROM usuario
                WHERE login = @login AND ativo = TRUE
                LIMIT 1";

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            using var comando = new MySqlCommand(sql, conexao);
            comando.Parameters.AddWithValue("@login", login);

            using var leitor = comando.ExecuteReader();

            if (!leitor.Read())
                return null;

            string hash = leitor.GetString("senha_hash");

            if (!SenhaService.Verificar(senha, hash))
                return null;

            return new Usuario
            {
                IdUsuario = leitor.GetInt32("id_usuario"),
                Nome = leitor.GetString("nome"),
                Login = leitor.GetString("login"),
                Perfil = leitor.GetString("perfil"),
                Ativo = leitor.GetBoolean("ativo")
            };
        }

        // Cria o administrador apenas quando não existem
        // usuários cadastrados no banco.
        public void CriarPrimeiroAdministrador(
            string nome, string login, string senha)
        {
            if (senha.Length < 12)
                throw new ArgumentException(
                    "A senha inicial deve ter pelo menos 12 caracteres.");

            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();

            using var transacao = conexao.BeginTransaction(
                System.Data.IsolationLevel.Serializable);

            // Bloqueia a tabela durante a verificação inicial.
            const string consulta = "SELECT COUNT(*) FROM usuario";

            using (var verificar =
                new MySqlCommand(consulta, conexao, transacao))
            {
                long total = Convert.ToInt64(
                    verificar.ExecuteScalar());

                if (total > 0)
                {
                    transacao.Rollback();
                    throw new InvalidOperationException(
                        "Já existe um usuário cadastrado.");
                }
            }

            const string sql = @"
                INSERT INTO usuario
                    (nome, login, senha_hash, perfil, ativo)
                VALUES
                    (@nome, @login, @hash, 'ADMINISTRADOR', TRUE)";

            using (var comando =
                new MySqlCommand(sql, conexao, transacao))
            {
                comando.Parameters.AddWithValue("@nome", nome);
                comando.Parameters.AddWithValue("@login", login);
                comando.Parameters.AddWithValue(
                    "@hash", SenhaService.GerarHash(senha));

                comando.ExecuteNonQuery();
            }

            transacao.Commit();
        }
    }
}
