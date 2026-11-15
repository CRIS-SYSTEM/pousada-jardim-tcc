using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CadastroProdutoWpf;

/// <summary>Gera relatórios PDF A4 com paginação, sem dependências externas.</summary>
internal static class RelatorioPdf
{
    private const double Largura = 595.28;
    private const double Altura = 841.89;
    private const double Margem = 42;
    private const double LinhaAltura = 17;
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    public static void Salvar(string arquivo, string titulo, string[] colunas, IEnumerable<string[]> dados)
    {
        var registros = dados.ToList();
        var paginas = new List<string>();
        var conteudo = new StringBuilder();
        int numeroPagina = 0;
        double y = 0;
        void NovaPagina()
        {
            if (numeroPagina > 0) paginas.Add(conteudo.ToString());
            numeroPagina++;
            conteudo = new StringBuilder();
            y = Altura - 51;
            Texto(conteudo, Margem, y, 17, titulo, true);
            y -= 24;
            Texto(conteudo, Margem, y, 9, "Pousada Jardim | Emitido em " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", Pt));
            y -= 23;
            Reta(conteudo, Margem, y, Largura - Margem, y, 0.7);
            y -= 21;
            Cabecalho();
        }
        void Cabecalho()
        {
            double largura = (Largura - 2 * Margem) / colunas.Length;
            for (int i = 0; i < colunas.Length; i++)
                Texto(conteudo, Margem + i * largura + 3, y, 8, Cortar(colunas[i], largura - 6, 8), true);
            y -= LinhaAltura;
            Reta(conteudo, Margem, y + 6, Largura - Margem, y + 6, 0.35);
        }
        NovaPagina();
        double coluna = (Largura - 2 * Margem) / colunas.Length;
        foreach (var registro in registros)
        {
            if (y < 60) NovaPagina();
            for (int i = 0; i < colunas.Length; i++)
                Texto(conteudo, Margem + i * coluna + 3, y, 8,
                    Cortar(i < registro.Length ? registro[i] : "", coluna - 6, 8));
            y -= LinhaAltura;
        }
        if (registros.Count == 0) Texto(conteudo, Margem + 3, y, 10, "Nenhum registro encontrado.");
        paginas.Add(conteudo.ToString());
        EscreverPdf(arquivo, paginas);
    }

    private static string Cortar(string? texto, double largura, int tamanho)
    {
        string valor = (texto ?? "").Replace('\n', ' ').Replace('\r', ' ');
        int max = Math.Max(4, (int)(largura / (tamanho * 0.51)));
        return valor.Length > max ? valor.Substring(0, max - 3) + "..." : valor;
    }
    private static void Reta(StringBuilder b, double x1, double y1, double x2, double y2, double largura)
        => b.AppendFormat(CultureInfo.InvariantCulture, "0.26 0.48 0.34 RG {0:F2} w {1:F2} {2:F2} m {3:F2} {4:F2} l S\n", largura, x1, y1, x2, y2);
    private static void Texto(StringBuilder b, double x, double y, int tamanho, string valor, bool negrito = false)
    {
        b.AppendFormat(CultureInfo.InvariantCulture, "BT /{0} {1} Tf {2:F2} {3:F2} Td (", negrito ? "F2" : "F1", tamanho, x, y);
        foreach (char c in valor)
        {
            if (c == '(' || c == ')' || c == '\\') b.Append('\\');
            if (c >= 32 && c <= 126) b.Append(c);
            else if (c >= 160 && c <= 255) b.Append('\\').Append(Convert.ToString((int)c, 8).PadLeft(3, '0'));
            else if (c == '€') b.Append("\\200");
            else if (c == '–' || c == '—') b.Append('-');
            else b.Append('?');
        }
        b.Append(") Tj ET\n");
    }
    private static void EscreverPdf(string arquivo, List<string> paginas)
    {
        var objetos = new List<byte[]> { Array.Empty<byte>() };
        int Adicionar(string s) { objetos.Add(Encoding.ASCII.GetBytes(s)); return objetos.Count - 1; }
        int catalogo = Adicionar("");
        int paginasId = Adicionar("");
        int normal = Adicionar("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        int bold = Adicionar("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        var ids = new List<int>();
        foreach (string pagina in paginas)
        {
            byte[] stream = Encoding.ASCII.GetBytes(pagina);
            int conteudo = objetos.Count;
            objetos.Add(Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n").Concat(stream).Concat(Encoding.ASCII.GetBytes("\nendstream")).ToArray());
            ids.Add(Adicionar($"<< /Type /Page /Parent {paginasId} 0 R /MediaBox [0 0 {Largura.ToString(CultureInfo.InvariantCulture)} {Altura.ToString(CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 {normal} 0 R /F2 {bold} 0 R >> >> /Contents {conteudo} 0 R >>"));
        }
        objetos[catalogo] = Encoding.ASCII.GetBytes($"<< /Type /Catalog /Pages {paginasId} 0 R >>");
        objetos[paginasId] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [{string.Join(" ", ids.Select(id => $"{id} 0 R"))}] /Count {ids.Count} >>");
        using var fs = new FileStream(arquivo, FileMode.Create, FileAccess.Write);
        void Escrever(string s) { byte[] b = Encoding.ASCII.GetBytes(s); fs.Write(b, 0, b.Length); }
        Escrever("%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (int i = 1; i < objetos.Count; i++)
        {
            offsets.Add(fs.Position);
            Escrever($"{i} 0 obj\n"); fs.Write(objetos[i], 0, objetos[i].Length); Escrever("\nendobj\n");
        }
        long xref = fs.Position;
        Escrever($"xref\n0 {objetos.Count}\n0000000000 65535 f \n");
        for (int i = 1; i < objetos.Count; i++) Escrever($"{offsets[i]:D10} 00000 n \n");
        Escrever($"trailer\n<< /Size {objetos.Count} /Root {catalogo} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
    }
}
