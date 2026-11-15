
using System;
using System.Security.Cryptography;

namespace CadastroProdutoWpf
{
    internal static class SenhaService
    {
        private const int Iteracoes = 220000;

        // Gera um hash seguro para armazenamento.
        public static string GerarHash(string senha)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                senha,
                salt,
                Iteracoes,
                HashAlgorithmName.SHA256,
                32);

            return Iteracoes + "." +
                   Convert.ToBase64String(salt) + "." +
                   Convert.ToBase64String(hash);
        }

        // Verifica se a senha corresponde ao hash salvo.
        public static bool Verificar(
            string senha, string hashArmazenado)
        {
            try
            {
                string[] partes = hashArmazenado.Split('.');

                if (partes.Length != 3)
                    return false;

                int iteracoes = int.Parse(partes[0]);

                if (iteracoes < 100000 || iteracoes > 1000000)
                    return false;

                byte[] salt = Convert.FromBase64String(partes[1]);
                byte[] hashOriginal =
                    Convert.FromBase64String(partes[2]);

                if (salt.Length != 16 ||
                    hashOriginal.Length != 32)
                    return false;

                byte[] hashInformado =
                    Rfc2898DeriveBytes.Pbkdf2(
                        senha,
                        salt,
                        iteracoes,
                        HashAlgorithmName.SHA256,
                        32);

                return CryptographicOperations.FixedTimeEquals(
                    hashOriginal, hashInformado);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
