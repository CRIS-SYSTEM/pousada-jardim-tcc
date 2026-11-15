
using System;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    internal static class ConexaoFactory
    {
        public static MySqlConnection CriarConexao()
        {
            string? senha = Environment.GetEnvironmentVariable(
                "MYSQL_PASSWORD");

            if (string.IsNullOrWhiteSpace(senha))
                throw new InvalidOperationException(
                    "A variável MYSQL_PASSWORD não está configurada.");

            var config = new MySqlConnectionStringBuilder
            {
                Server = "localhost",
                Port = 3306,
                Database = "estoque_teste",
                UserID = "app_estoque",
                Password = senha
            };

            return new MySqlConnection(config.ConnectionString);
        }
    }
}
