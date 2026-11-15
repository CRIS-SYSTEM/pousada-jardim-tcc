using System;
using System.Collections.Generic;
using MySqlConnector;

namespace CadastroProdutoWpf;

internal sealed class UsuariosDAO
{
    public List<Usuario> Listar()
    {
        var usuarios = new List<Usuario>();
        using var conexao = ConexaoFactory.CriarConexao();
        conexao.Open();
        using var cmd = new MySqlCommand("SELECT id_usuario,nome,login,perfil,ativo FROM usuario ORDER BY nome", conexao);
        using var leitor = cmd.ExecuteReader();
        while (leitor.Read()) usuarios.Add(new Usuario
        {
            IdUsuario = leitor.GetInt32("id_usuario"), Nome = leitor.GetString("nome"),
            Login = leitor.GetString("login"), Perfil = leitor.GetString("perfil"),
            Ativo = leitor.GetBoolean("ativo")
        });
        return usuarios;
    }

    public void Salvar(Usuario usuario, string? novaSenha, int administradorId)
    {
        if (string.IsNullOrWhiteSpace(usuario.Nome) || string.IsNullOrWhiteSpace(usuario.Login))
            throw new ArgumentException("Informe nome e login.");
        if (usuario.Nome.Trim().Length > 100 || usuario.Login.Trim().Length > 50)
            throw new ArgumentException("Nome ou login excede o tamanho permitido.");
        if (usuario.Perfil != "ADMINISTRADOR" && usuario.Perfil != "FUNCIONARIO")
            throw new ArgumentException("Perfil inválido.");
        if (!string.IsNullOrEmpty(novaSenha) && novaSenha.Length < 12)
            throw new ArgumentException("A senha deve conter pelo menos 12 caracteres.");
        if (usuario.IdUsuario == 0 && string.IsNullOrEmpty(novaSenha))
            throw new ArgumentException("Informe uma senha para o novo usuário.");

        using var conexao = ConexaoFactory.CriarConexao();
        conexao.Open();
        using var tx = conexao.BeginTransaction();
        GarantirAdministrador(conexao, tx, administradorId);
        if (usuario.IdUsuario > 0)
        {
            // Evita que o administrador retire o próprio acesso durante a sessão.
            if (usuario.IdUsuario == administradorId && (!usuario.Ativo || usuario.Perfil != "ADMINISTRADOR"))
                throw new InvalidOperationException("Não é permitido inativar ou rebaixar seu próprio usuário.");
            using var atual = new MySqlCommand("SELECT perfil,ativo FROM usuario WHERE id_usuario=@id FOR UPDATE", conexao, tx);
            atual.Parameters.AddWithValue("@id", usuario.IdUsuario);
            using (var leitor = atual.ExecuteReader())
                if (!leitor.Read()) throw new InvalidOperationException("Usuário não encontrado.");
            using var cmd = new MySqlCommand(@"UPDATE usuario SET nome=@nome,login=@login,perfil=@perfil,ativo=@ativo WHERE id_usuario=@id", conexao, tx);
            AdicionarParametros(cmd, usuario);
            cmd.ExecuteNonQuery();
            if (!string.IsNullOrEmpty(novaSenha))
            {
                using var senha = new MySqlCommand("UPDATE usuario SET senha_hash=@hash WHERE id_usuario=@id", conexao, tx);
                senha.Parameters.AddWithValue("@hash", SenhaService.GerarHash(novaSenha));
                senha.Parameters.AddWithValue("@id", usuario.IdUsuario);
                senha.ExecuteNonQuery();
            }
        }
        else
        {
            using var cmd = new MySqlCommand(@"INSERT INTO usuario(nome,login,perfil,ativo,senha_hash) VALUES(@nome,@login,@perfil,@ativo,@hash)", conexao, tx);
            AdicionarParametros(cmd, usuario);
            cmd.Parameters.AddWithValue("@hash", SenhaService.GerarHash(novaSenha!));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    private static void AdicionarParametros(MySqlCommand cmd, Usuario usuario)
    {
        cmd.Parameters.AddWithValue("@id", usuario.IdUsuario);
        cmd.Parameters.AddWithValue("@nome", usuario.Nome.Trim());
        cmd.Parameters.AddWithValue("@login", usuario.Login.Trim());
        cmd.Parameters.AddWithValue("@perfil", usuario.Perfil);
        cmd.Parameters.AddWithValue("@ativo", usuario.Ativo);
    }

    private static void GarantirAdministrador(MySqlConnection conexao, MySqlTransaction tx, int id)
    {
        using var cmd = new MySqlCommand("SELECT COUNT(*) FROM usuario WHERE id_usuario=@id AND perfil='ADMINISTRADOR' AND ativo=1", conexao, tx);
        cmd.Parameters.AddWithValue("@id", id);
        if (Convert.ToInt32(cmd.ExecuteScalar()) != 1)
            throw new UnauthorizedAccessException("A operação exige um administrador ativo.");
    }
}
