using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CadastroProdutoWpf;

internal sealed record RequisicaoItem(string Nome, string Unidade);

internal static class RequisicaoFormularioPdf
{
    private const double W = 595.28, H = 841.89;
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
    private static string Esc(string value)
    {
        var b = new StringBuilder();
        foreach (char c in value ?? "")
        {
            if (c == '(' || c == ')' || c == '\\') b.Append('\\');
            if (c >= 32 && c <= 126) b.Append(c);
            else if (c >= 160 && c <= 255) b.Append('\\').Append(Convert.ToString((int)c, 8).PadLeft(3, '0'));
            else b.Append('?');
        }
        return b.ToString();
    }
    private static void Txt(StringBuilder b, double x, double y, string text, int size = 9, bool bold = false)
        => b.AppendFormat(CultureInfo.InvariantCulture, "BT /{0} {1} Tf {2:F2} {3:F2} Td ({4}) Tj ET\n", bold ? "F2" : "F1", size, x, y, Esc(text));
    private static void Line(StringBuilder b, double x1, double y1, double x2, double y2)
        => b.AppendFormat(CultureInfo.InvariantCulture, "0.20 0.38 0.27 RG 0.6 w {0:F2} {1:F2} m {2:F2} {3:F2} l S\n", x1, y1, x2, y2);
    public static void Gerar(string arquivo, bool saida, DateTime emissao,
        DateTime? efetiva, string setor, string solicitante, string periodo,
        string funcionario, string entrega, string lancamento, IReadOnlyList<RequisicaoItem>? itens = null)
    {
        const int porPagina = 14;
        int totalPaginas = Math.Max(1, (int)Math.Ceiling((itens?.Count ?? 0) / (double)porPagina));
        var paginas = new List<string>();
        for (int pagina = 0; pagina < totalPaginas; pagina++)
        {
        var b = new StringBuilder();
        Txt(b, 42, 797, "POUSADA JARDIM", 17, true);
        Txt(b, 42, 778, "Controle de Estoque Setorizado", 9);
        Txt(b, 320, 797, saida ? "REQUISICAO DE SAIDA" : "REQUISICAO DE ENTRADA", 12, true);
        Line(b, 42, 764, 553, 764);
        Txt(b, 42, 742, "Data da requisicao: " + emissao.ToString("dd/MM/yyyy", Br));
        Txt(b, 42, 723, (saida ? "Data da retirada: " : "Data do recebimento: ") + (efetiva?.ToString("dd/MM/yyyy", Br) ?? "____/____/______"));
        Txt(b, 42, 704, "Setor: " + setor);
        Txt(b, 295, 704, "Setor solicitante: " + solicitante);
        Txt(b, 42, 685, "Periodo: " + periodo);
        Txt(b, 42, 666, "Funcionario que retira / recebe: " + funcionario);
        Txt(b, 42, 647, "Responsavel pela entrega: " + entrega);
        Txt(b, 42, 628, "Responsavel pelo lancamento: " + lancamento);
        Line(b, 42, 612, 553, 612);
        string[] headers = saida ? new[] { "Lote", "Nome do produto", "Qtd.", "Validade" }
                                 : new[] { "Lote", "Nome do produto", "Qtd.", "Validade", "Unid.", "Preco", "Total" };
        double[] widths = saida ? new[] { 88d, 232d, 78d, 113d }
                                : new[] { 65d, 163d, 48d, 81d, 44d, 55d, 55d };
        double x = 42;
        for (int i = 0; i < headers.Length; i++)
        {
            Txt(b, x + 3, 590, headers[i], 8, true);
            x += widths[i];
        }
        Line(b, 42, 579, 553, 579);
        int inicio = pagina * porPagina;
        int quantidadePagina = Math.Min(porPagina, Math.Max(0, (itens?.Count ?? 0) - inicio));
        for (int row = 0; row < quantidadePagina; row++)
        {
            var item = itens![inicio + row];
            double y = 563 - row * 24;
            // A quantidade, o lote e a validade permanecem em branco para preenchimento físico.
            Txt(b, saida ? 133 : 110, y, item.Nome.Length > (saida ? 37 : 25)
                ? item.Nome.Substring(0, saida ? 34 : 22) + "..." : item.Nome, 8);
            if (!saida) Txt(b, 401, y, item.Unidade.Length > 6 ? item.Unidade.Substring(0, 6) : item.Unidade, 7);
        }
        for (int row = 0; row < 14; row++)
        {
            double y = 579 - row * 24;
            Line(b, 42, y - 24, 553, y - 24);
        }
        x = 42;
        Line(b, x, 579, x, 243);
        foreach (double width in widths)
        {
            x += width;
            Line(b, x, 579, x, 243);
        }
        Txt(b, 42, 218, "Observacoes: ______________________________________________________________");
        Txt(b, 42, 200, "__________________________________________________________________________");
        Line(b, 42, 132, 192, 132);
        Line(b, 222, 132, 372, 132);
        Line(b, 402, 132, 552, 132);
        Txt(b, 42, 116, "Retirada / recebimento", 8);
        Txt(b, 222, 116, "Entrega", 8);
        Txt(b, 402, 116, "Lancamento no sistema", 8);
        Txt(b, 42, 79, "Documento de controle fisico. Nao altera o estoque automaticamente.", 8);
        Txt(b, 490, 79, $"Pag. {pagina + 1}/{totalPaginas}", 8);
        paginas.Add(b.ToString());
        }
        Write(arquivo, paginas);
    }
    private static void Write(string path, IReadOnlyList<string> contents)
    {
        var objects = new List<byte[]> { Array.Empty<byte>() };
        int Add(string s) { objects.Add(Encoding.ASCII.GetBytes(s)); return objects.Count - 1; }
        int catalog = Add(""), pages = Add("");
        int font1 = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        int font2 = Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        var pageIds = new List<int>();
        foreach (string content in contents)
        {
            byte[] stream = Encoding.ASCII.GetBytes(content);
            int data = objects.Count;
            objects.Add(Encoding.ASCII.GetBytes($"<< /Length {stream.Length} >>\nstream\n")
                .Concat(stream).Concat(Encoding.ASCII.GetBytes("\nendstream")).ToArray());
            int page = Add($"<< /Type /Page /Parent {pages} 0 R /MediaBox [0 0 {W.ToString(CultureInfo.InvariantCulture)} {H.ToString(CultureInfo.InvariantCulture)}] /Resources << /Font << /F1 {font1} 0 R /F2 {font2} 0 R >> >> /Contents {data} 0 R >>");
            pageIds.Add(page);
        }
        objects[catalog] = Encoding.ASCII.GetBytes($"<< /Type /Catalog /Pages {pages} 0 R >>");
        objects[pages] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pageIds.Count} >>");
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        void Put(string s) { var bytes = Encoding.ASCII.GetBytes(s); fs.Write(bytes, 0, bytes.Length); }
        Put("%PDF-1.4\n");
        var offsets = new List<long> { 0 };
        for (int i = 1; i < objects.Count; i++)
        {
            offsets.Add(fs.Position);
            Put($"{i} 0 obj\n"); fs.Write(objects[i], 0, objects[i].Length); Put("\nendobj\n");
        }
        long xref = fs.Position;
        Put($"xref\n0 {objects.Count}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Put($"{offset:D10} 00000 n \n");
        Put($"trailer\n<< /Size {objects.Count} /Root {catalog} 0 R >>\nstartxref\n{xref}\n%%EOF");
    }
}
