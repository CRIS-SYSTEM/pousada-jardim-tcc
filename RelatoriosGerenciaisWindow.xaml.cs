using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using MySqlConnector;

namespace CadastroProdutoWpf;

public partial class RelatoriosGerenciaisWindow : Window
{
    private sealed class Opcao
    {
        public string Nome { get; set; } = "";
        public int Id { get; set; }
        public override string ToString() => Nome;
    }
    private sealed class Linha
    {
        public int ProdutoId { get; set; }
        public int SetorId { get; set; }
        public int? CategoriaId { get; set; }
        public string Produto { get; set; } = "";
        public string Categoria { get; set; } = "";
        public string Setor { get; set; } = "";
        public decimal Quantidade { get; set; }
        public decimal? Minimo { get; set; }
        public string Lote { get; set; } = "";
        public DateTime? Vencimento { get; set; }
        public string Situacao { get; set; } = "";
        public string Validade { get; set; } = "";
        public string QuantidadeFormatada => Quantidade.ToString("N2", CultureInfo.GetCultureInfo("pt-BR"));
        public string MinimoFormatado => Minimo?.ToString("N2", CultureInfo.GetCultureInfo("pt-BR")) ?? "—";
        public string VencimentoFormatado => Vencimento?.ToString("dd/MM/yyyy") ?? "—";
    }

    private sealed record Coluna(string Chave, string Titulo, string Binding, double Largura);
    private static readonly Coluna[] Colunas =
    {
        new("Produto", "Produto", "Produto", 230), new("Categoria", "Categoria", "Categoria", 130),
        new("Setor", "Setor", "Setor", 150), new("Quantidade", "Quantidade", "QuantidadeFormatada", 105),
        new("Minimo", "Estoque mínimo", "MinimoFormatado", 120), new("Situacao", "Situação", "Situacao", 160),
        new("Lote", "Lote", "Lote", 120), new("Vencimento", "Vencimento", "VencimentoFormatado", 120),
        new("Validade", "Situação da validade", "Validade", 160)
    };
    private readonly HashSet<string> colunasSelecionadas = new(new[] { "Produto", "Categoria", "Setor", "Quantidade", "Minimo", "Situacao" });
    private readonly List<Opcao> produtos = new();
    private readonly List<Linha> estoque = new();
    private readonly List<Linha> lotes = new();
    private readonly List<Linha> visiveis = new();
    private bool carregando;
    private bool escolhendoSugestao;
    private int? produtoSelecionado;
    private int dias = 30;

    public RelatoriosGerenciaisWindow()
    {
        InitializeComponent();
        carregando = true;
        cmbSituacaoFiltro.ItemsSource = new[] { "Todas", "Normal", "Abaixo ou no mínimo", "Não configurado" };
        cmbSituacaoFiltro.SelectedIndex = 0;
        cmbValidadeFiltro.ItemsSource = new[] { "Todas", "No prazo", "Próximo do vencimento", "Vencido", "Não informado" };
        cmbValidadeFiltro.SelectedIndex = 0;
        CriarOpcoesColunas();
        carregando = false;
        Loaded += (_, _) => Atualizar();
    }

    private void CriarOpcoesColunas()
    {
        painelColunas.Children.Clear();
        foreach (var coluna in Colunas)
        {
            var ck = new CheckBox { Content = coluna.Titulo, Tag = coluna.Chave, Margin = new Thickness(0, 3, 0, 3),
                IsChecked = colunasSelecionadas.Contains(coluna.Chave) };
            ck.Checked += ColunaAlterada;
            ck.Unchecked += ColunaAlterada;
            painelColunas.Children.Add(ck);
        }
    }
    private void ColunaAlterada(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox ck || ck.Tag is not string chave) return;
        if (ck.IsChecked == true) colunasSelecionadas.Add(chave);
        else
        {
            if (colunasSelecionadas.Count == 1) { ck.IsChecked = true; return; }
            colunasSelecionadas.Remove(chave);
        }
        AplicarFiltros();
    }
    private void BtnColunas_Click(object sender, RoutedEventArgs e) => popupColunas.IsOpen = !popupColunas.IsOpen;
    private void CardEstoque_Click(object sender, RoutedEventArgs e) => abas.SelectedIndex = 0;
    private void CardMinimo_Click(object sender, RoutedEventArgs e) => abas.SelectedIndex = 1;
    private void CardVencimentos_Click(object sender, RoutedEventArgs e) => abas.SelectedIndex = 2;
    private void Abas_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == abas && !carregando) AplicarFiltros();
    }
    private void BtnAtualizar_Click(object sender, RoutedEventArgs e) => Atualizar();
    private void BtnHistorico_Click(object sender, RoutedEventArgs e)
    {
        var janela = new HistoricoWindow();
        if (IsVisible) janela.Owner = this;
        janela.ShowDialog();
    }
    private void TxtDias_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(txtDias.Text, out int novo) && novo is >= 0 and <= 3650)
        { dias = novo; ReclassificarValidades(); AplicarFiltros(); }
        else { txtDias.Text = dias.ToString(); MessageBox.Show("Informe um prazo entre 0 e 3650 dias."); }
    }
    private void TxtProdutoFiltro_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (carregando || escolhendoSugestao) return;
        produtoSelecionado = null;
        string busca = txtProdutoFiltro.Text.Trim();
        var sugestoes = produtos.Where(p => p.Nome.Contains(busca, StringComparison.CurrentCultureIgnoreCase))
            .Take(8).ToList();
        listaSugestoes.ItemsSource = sugestoes;
        popupSugestoes.IsOpen = busca.Length > 0 && sugestoes.Count > 0;
        AplicarFiltros();
    }
    private void ListaSugestoes_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (listaSugestoes.SelectedItem is not Opcao opcao) return;
        escolhendoSugestao = true;
        try { txtProdutoFiltro.Text = opcao.Nome; txtProdutoFiltro.CaretIndex = txtProdutoFiltro.Text.Length;
            produtoSelecionado = opcao.Id; popupSugestoes.IsOpen = false; }
        finally { escolhendoSugestao = false; }
        listaSugestoes.SelectedItem = null;
        AplicarFiltros();
    }
    private void Filtro_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!carregando) AplicarFiltros();
    }
    private void BtnLimparFiltros_Click(object sender, RoutedEventArgs e)
    {
        carregando = true;
        txtProdutoFiltro.Clear(); produtoSelecionado = null;
        cmbCategoriaFiltro.SelectedIndex = 0; cmbSetorFiltro.SelectedIndex = 0;
        cmbSituacaoFiltro.SelectedIndex = 0; cmbValidadeFiltro.SelectedIndex = 0;
        popupSugestoes.IsOpen = false;
        carregando = false;
        AplicarFiltros();
    }
    private void Atualizar()
    {
        if (!int.TryParse(txtDias.Text, out int novo) || novo < 0 || novo > 3650)
        { MessageBox.Show("Informe um prazo de vencimento entre 0 e 3650 dias."); return; }
        dias = novo;
        try
        {
            var novosEstoques = new List<Linha>();
            var novosLotes = new List<Linha>();
            var novosProdutos = new List<Opcao>();
            var categorias = new List<Opcao> { new() { Id = 0, Nome = "Todas" } };
            var setores = new List<Opcao> { new() { Id = 0, Nome = "Todos" } };
            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();
            const string sqlEstoque = @"SELECT p.id_produto, p.nome AS produto,
    p.id_categoria, COALESCE(c.nome, 'Sem categoria') AS categoria,
    s.id_setor, s.nome AS setor, ps.quantidade_disponivel, ps.estoque_minimo
FROM produto_setor ps JOIN produto p ON p.id_produto = ps.id_produto
JOIN setor s ON s.id_setor = ps.id_setor
LEFT JOIN categoria c ON c.id_categoria = p.id_categoria
ORDER BY s.nome, p.nome";
            using (var cmd = new MySqlCommand(sqlEstoque, conexao))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                {
                    var qtd = reader.GetDecimal("quantidade_disponivel");
                    decimal? minimo = reader.IsDBNull(reader.GetOrdinal("estoque_minimo")) ? null : reader.GetDecimal("estoque_minimo");
                    novosEstoques.Add(new Linha
                    {
                        ProdutoId = reader.GetInt32("id_produto"), Produto = reader.GetString("produto"),
                        CategoriaId = reader.IsDBNull(reader.GetOrdinal("id_categoria")) ? null : reader.GetInt32("id_categoria"),
                        Categoria = reader.GetString("categoria"), SetorId = reader.GetInt32("id_setor"),
                        Setor = reader.GetString("setor"), Quantidade = qtd, Minimo = minimo,
                        Situacao = !minimo.HasValue ? "Não configurado" : qtd <= minimo.Value ? "Abaixo ou no mínimo" : "Normal"
                    });
                }
            const string sqlLotes = @"SELECT p.id_produto, p.nome AS produto, p.id_categoria,
    COALESCE(c.nome, 'Sem categoria') AS categoria,
    s.id_setor, s.nome AS setor,
    COALESCE(NULLIF(lp.numero_lote,''), LPAD(lp.id_lote,3,'0')) AS lote,
    lp.data_vencimento, els.quantidade_disponivel,
    ps.estoque_minimo, ps.quantidade_disponivel AS saldo_setor
FROM estoque_lote_setor els
JOIN produto_setor ps ON ps.id_produto_setor = els.id_produto_setor
JOIN produto p ON p.id_produto = ps.id_produto
JOIN setor s ON s.id_setor = ps.id_setor
LEFT JOIN categoria c ON c.id_categoria = p.id_categoria
JOIN lote_produto lp ON lp.id_lote = els.id_lote AND lp.id_produto = ps.id_produto
WHERE els.quantidade_disponivel > 0
ORDER BY lp.data_vencimento IS NULL, lp.data_vencimento, p.nome, s.nome";
            using (var cmd = new MySqlCommand(sqlLotes, conexao))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                {
                    decimal? minimo = reader.IsDBNull(reader.GetOrdinal("estoque_minimo")) ? null : reader.GetDecimal("estoque_minimo");
                    decimal saldoSetor = reader.GetDecimal("saldo_setor");
                    novosLotes.Add(new Linha
                    {
                        ProdutoId = reader.GetInt32("id_produto"), Produto = reader.GetString("produto"),
                        CategoriaId = reader.IsDBNull(reader.GetOrdinal("id_categoria")) ? null : reader.GetInt32("id_categoria"),
                        Categoria = reader.GetString("categoria"), SetorId = reader.GetInt32("id_setor"),
                        Setor = reader.GetString("setor"), Lote = reader.GetString("lote"),
                        Quantidade = reader.GetDecimal("quantidade_disponivel"), Minimo = minimo,
                        Vencimento = reader.IsDBNull(reader.GetOrdinal("data_vencimento")) ? null : reader.GetDateTime("data_vencimento"),
                        // Minimum situation refers to the aggregate sector balance, not this lot's quantity.
                        Situacao = !minimo.HasValue ? "Não configurado" : saldoSetor <= minimo.Value ? "Abaixo ou no mínimo" : "Normal"
                    });
                }
            using (var cmd = new MySqlCommand("SELECT id_produto, nome FROM produto ORDER BY nome", conexao))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read()) novosProdutos.Add(new Opcao { Id = reader.GetInt32("id_produto"), Nome = reader.GetString("nome") });
            using (var cmd = new MySqlCommand("SELECT id_categoria, nome FROM categoria ORDER BY nome", conexao))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read()) categorias.Add(new Opcao { Id = reader.GetInt32("id_categoria"), Nome = reader.GetString("nome") });
            using (var cmd = new MySqlCommand("SELECT id_setor, nome FROM setor ORDER BY nome", conexao))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read()) setores.Add(new Opcao { Id = reader.GetInt32("id_setor"), Nome = reader.GetString("nome") });
            estoque.Clear(); estoque.AddRange(novosEstoques);
            lotes.Clear(); lotes.AddRange(novosLotes);
            produtos.Clear(); produtos.AddRange(novosProdutos);
            ReclassificarValidades();
            int? categoriaAtual = (cmbCategoriaFiltro.SelectedItem as Opcao)?.Id;
            int? setorAtual = (cmbSetorFiltro.SelectedItem as Opcao)?.Id;
            carregando = true;
            cmbCategoriaFiltro.ItemsSource = categorias;
            cmbSetorFiltro.ItemsSource = setores;
            cmbCategoriaFiltro.SelectedItem = categorias.FirstOrDefault(x => x.Id == categoriaAtual) ?? categorias[0];
            cmbSetorFiltro.SelectedItem = setores.FirstOrDefault(x => x.Id == setorAtual) ?? setores[0];
            carregando = false;
            AplicarFiltros();
        }
        catch (Exception ex)
        {
            carregando = false;
            MessageBox.Show("Não foi possível carregar os relatórios.\n" + ex.Message,
                "Pousada Jardim", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void ReclassificarValidades()
    {
        foreach (var linha in lotes)
            linha.Validade = !linha.Vencimento.HasValue ? "Não informado" :
                linha.Vencimento.Value.Date < DateTime.Today ? "Vencido" :
                linha.Vencimento.Value.Date <= DateTime.Today.AddDays(dias) ? "Próximo do vencimento" : "No prazo";
    }
    private void AplicarFiltros()
    {
        if (carregando || gridEstoque is null || abas is null) return;
        var tab = abas.SelectedIndex;
        if (tab < 0) return;
        var fonte = tab == 2 ? lotes : estoque;
        string nome = txtProdutoFiltro?.Text.Trim() ?? "";
        int? catId = (cmbCategoriaFiltro?.SelectedItem as Opcao)?.Id;
        int? setorId = (cmbSetorFiltro?.SelectedItem as Opcao)?.Id;
        string sit = cmbSituacaoFiltro?.SelectedItem as string ?? "Todas";
        string validade = cmbValidadeFiltro?.SelectedItem as string ?? "Todas";
        visiveis.Clear();
        foreach (var linha in fonte)
        {
            if (tab == 1 && linha.Situacao != "Abaixo ou no mínimo") continue;
            if (produtoSelecionado.HasValue && linha.ProdutoId != produtoSelecionado.Value) continue;
            if (!produtoSelecionado.HasValue && !linha.Produto.Contains(nome, StringComparison.CurrentCultureIgnoreCase)) continue;
            if (catId.GetValueOrDefault() != 0 && linha.CategoriaId != catId) continue;
            if (setorId.GetValueOrDefault() != 0 && linha.SetorId != setorId) continue;
            if (sit != "Todas" && linha.Situacao != sit) continue;
            if (validade != "Todas")
            {
                // In stock reports, a sector appears if at least one active lot meets the validity filter.
                if (tab == 2 && linha.Validade != validade) continue;
                if (tab != 2 && !lotes.Any(l => l.ProdutoId == linha.ProdutoId && l.SetorId == linha.SetorId && l.Validade == validade)) continue;
            }
            visiveis.Add(linha);
        }
        var grid = tab == 2 ? gridVencimentos : tab == 1 ? gridMinimos : gridEstoque;
        ConstruirColunas(grid, tab);
        gridEstoque.ItemsSource = tab == 0 ? visiveis.ToList() : null;
        gridMinimos.ItemsSource = tab == 1 ? visiveis.ToList() : null;
        gridVencimentos.ItemsSource = tab == 2 ? visiveis.ToList() : null;
        lblResultados.Text = $"{visiveis.Count} registro(s) • Filtros combináveis";
    }
    private void ConstruirColunas(DataGrid grid, int tab)
    {
        grid.Columns.Clear();
        foreach (var c in Colunas)
        {
            if (!colunasSelecionadas.Contains(c.Chave)) continue;
            // Lot-specific fields are not emitted in aggregate sector reports.
            if (tab != 2 && (c.Chave is "Lote" or "Vencimento" or "Validade")) continue;
            grid.Columns.Add(new DataGridTextColumn { Header = c.Titulo, Binding = new Binding(c.Binding),
                Width = new DataGridLength(c.Largura), IsReadOnly = true });
        }
    }
    private (string Titulo, string[] Cabecalho, List<string[]> Linhas) ExportacaoAtual()
    {
        AplicarFiltros();
        int tab = abas.SelectedIndex;
        string titulo = tab == 2 ? "Vencimentos por lote" : tab == 1 ? "Estoque abaixo do mínimo" : "Estoque por setor";
        var col = Colunas.Where(c => colunasSelecionadas.Contains(c.Chave) &&
            (tab == 2 || c.Chave is not ("Lote" or "Vencimento" or "Validade"))).ToList();
        string[] valores(Linha x) => col.Select(c => c.Chave switch
        {
            "Produto" => x.Produto, "Categoria" => x.Categoria, "Setor" => x.Setor,
            "Quantidade" => x.QuantidadeFormatada, "Minimo" => x.MinimoFormatado,
            "Situacao" => x.Situacao, "Lote" => x.Lote,
            "Vencimento" => x.VencimentoFormatado, "Validade" => x.Validade, _ => ""
        }).ToArray();
        return (titulo, col.Select(c => c.Titulo).ToArray(), visiveis.Select(valores).ToList());
    }
    private void BtnPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new SaveFileDialog { Title = "Salvar relatório em PDF", Filter = "Documento PDF (*.pdf)|*.pdf",
            FileName = $"relatorio_pousada_jardim_{DateTime.Today:yyyyMMdd}.pdf" };
        try
        {
            // RelatoriosGerenciaisWindow e incorporada ao painel, nao e uma Window exibida.
            // Nao a utilize como proprietaria do dialogo nativo.
            if (dialogo.ShowDialog() != true) return;
            var rel = ExportacaoAtual();
            RelatorioPdf.Salvar(dialogo.FileName, rel.Titulo, rel.Cabecalho, rel.Linhas);
            MessageBox.Show("PDF salvo com sucesso.", "Pousada Jardim");
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível gerar o PDF: " + ex.Message); }
    }
    private static string Campo(string? texto)
    {
        string valor = texto ?? "";
        if (valor.Length > 0 && "=+-@".Contains(valor[0])) valor = "'" + valor;
        return "\"" + valor.Replace("\"", "\"\"") + "\"";
    }
    private void BtnExportar_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new SaveFileDialog { Title = "Exportar relatório", Filter = "Arquivo CSV (*.csv)|*.csv",
            FileName = $"relatorio_pousada_jardim_{DateTime.Today:yyyyMMdd}.csv" };
        try
        {
            // RelatoriosGerenciaisWindow e incorporada ao painel, nao e uma Window exibida.
            // Nao a utilize como proprietaria do dialogo nativo.
            if (dialogo.ShowDialog() != true) return;
            var rel = ExportacaoAtual();
            var linhas = new List<string> { string.Join(";", rel.Cabecalho.Select(Campo)) };
            linhas.AddRange(rel.Linhas.Select(l => string.Join(";", l.Select(Campo))));
            File.WriteAllLines(dialogo.FileName, linhas, new UTF8Encoding(true));
            MessageBox.Show("Relatório exportado com sucesso.");
        }
        catch (Exception ex) { MessageBox.Show("Falha ao exportar o relatório: " + ex.Message); }
    }
    private void BtnImprimir_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var rel = ExportacaoAtual();
            var documento = new FlowDocument { PagePadding = new Thickness(36), FontFamily = new FontFamily("Segoe UI"), FontSize = 10 };
            documento.Blocks.Add(new Paragraph(new Run("POUSADA JARDIM")) { FontSize = 17, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkGreen });
            documento.Blocks.Add(new Paragraph(new Run(rel.Titulo + " • " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"))) { FontSize = 12 });
            var tabela = new Table { CellSpacing = 0 };
            foreach (string _ in rel.Cabecalho) tabela.Columns.Add(new TableColumn());
            var grupo = new TableRowGroup();
            var cab = new TableRow { Background = new SolidColorBrush(Color.FromRgb(221, 240, 226)) };
            foreach (string c in rel.Cabecalho) cab.Cells.Add(new TableCell(new Paragraph(new Run(c)) { FontWeight = FontWeights.Bold }) { Padding = new Thickness(5) });
            grupo.Rows.Add(cab);
            foreach (var linha in rel.Linhas)
            {
                var tr = new TableRow();
                foreach (var campo in linha) tr.Cells.Add(new TableCell(new Paragraph(new Run(campo)))
                { Padding = new Thickness(5), BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 0.5) });
                grupo.Rows.Add(tr);
            }
            tabela.RowGroups.Add(grupo);
            documento.Blocks.Add(tabela);
            var dialogo = new PrintDialog();
            if (dialogo.ShowDialog() == true)
            {
                documento.PageWidth = dialogo.PrintableAreaWidth;
                documento.PageHeight = dialogo.PrintableAreaHeight;
                documento.ColumnWidth = dialogo.PrintableAreaWidth;
                dialogo.PrintDocument(((IDocumentPaginatorSource)documento).DocumentPaginator, rel.Titulo);
            }
        }
        catch (Exception ex) { MessageBox.Show("Falha na impressão: " + ex.Message, "Pousada Jardim"); }
    }
}
