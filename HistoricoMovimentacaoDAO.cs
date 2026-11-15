using System;
using System.Collections.Generic;
using MySqlConnector;

namespace CadastroProdutoWpf;

public sealed class HistoricoMovimentacaoItem
{
    public int Codigo { get; set; }
    public DateTime Data { get; set; }
    public string Tipo { get; set; } = "";
    public string Produto { get; set; } = "";
    public string Origem { get; set; } = "";
    public string Destino { get; set; } = "";
    public string Lote { get; set; } = "";
    public decimal Quantidade { get; set; }
    public string Usuario { get; set; } = "";
}

internal sealed class HistoricoMovimentacaoDAO
{
    public List<HistoricoMovimentacaoItem> Consultar(DateTime inicio, DateTime fimExclusivo, string? tipo)
    {
        const string sql = @"SELECT m.id_movimentacao,m.data_movimentacao,m.tipo,p.nome AS produto,
 s.nome AS origem,COALESCE(sd.nome,'') AS destino,COALESCE(NULLIF(l.numero_lote,''),LPAD(l.id_lote,3,'0'),'Sem lote') AS lote,
 m.quantidade,u.nome AS usuario
 FROM movimentacao m
 INNER JOIN produto_setor ps ON ps.id_produto_setor=m.id_produto_setor
 INNER JOIN produto p ON p.id_produto=ps.id_produto
 INNER JOIN setor s ON s.id_setor=ps.id_setor
 LEFT JOIN produto_setor pd ON pd.id_produto_setor=m.id_produto_setor_destino
 LEFT JOIN setor sd ON sd.id_setor=pd.id_setor
 LEFT JOIN lote_produto l ON l.id_lote=m.id_lote
 INNER JOIN usuario u ON u.id_usuario=m.id_usuario
 WHERE m.data_movimentacao >= @inicio AND m.data_movimentacao < @fim
 AND (@tipo IS NULL OR m.tipo=@tipo)
 ORDER BY m.data_movimentacao DESC,m.id_movimentacao DESC";
        var itens = new List<HistoricoMovimentacaoItem>();
        using var conexao = ConexaoFactory.CriarConexao(); conexao.Open();
        using var cmd = new MySqlCommand(sql, conexao);
        cmd.Parameters.AddWithValue("@inicio", inicio);
        cmd.Parameters.AddWithValue("@fim", fimExclusivo);
        cmd.Parameters.AddWithValue("@tipo", (object?)tipo ?? DBNull.Value);
        using var leitor = cmd.ExecuteReader();
        while (leitor.Read()) itens.Add(new HistoricoMovimentacaoItem
        {
            Codigo=leitor.GetInt32("id_movimentacao"), Data=leitor.GetDateTime("data_movimentacao"),
            Tipo=leitor.GetString("tipo"), Produto=leitor.GetString("produto"),
            Origem=leitor.GetString("origem"), Destino=leitor.GetString("destino"),
            Lote=leitor.GetString("lote"), Quantidade=leitor.GetDecimal("quantidade"),
            Usuario=leitor.GetString("usuario")
        });
        return itens;
    }
}
