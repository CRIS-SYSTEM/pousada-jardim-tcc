using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MySqlConnector;

namespace CadastroProdutoWpf;

public partial class RequisicoesWindow : Window
{
    private sealed record ProdutoSelecao(string Nome, string Unidade)
    {
        public string Descricao => $"{Nome} ({Unidade})";
    }

    public RequisicoesWindow()
    {
        InitializeComponent();
        dpEmissao.SelectedDate = DateTime.Today;
        AtualizarContagem();
    }

    private static List<ProdutoSelecao> ConsultarProdutos(bool saida, string setor)
    {
        if (saida && string.IsNullOrWhiteSpace(setor))
            throw new InvalidOperationException("Informe o nome exato do setor de origem.");
        var resultado = new List<ProdutoSelecao>();
        using var conexao = ConexaoFactory.CriarConexao();
        conexao.Open();
        string sql = saida
            ? @"SELECT DISTINCT p.nome, p.Unidade_Medida
                FROM produto p
                INNER JOIN produto_setor ps ON ps.id_produto = p.id_produto
                INNER JOIN setor s ON s.id_setor = ps.id_setor
                WHERE s.nome = @setor AND ps.quantidade_disponivel > 0
                ORDER BY p.nome"
            : @"SELECT p.nome, p.Unidade_Medida FROM produto p ORDER BY p.nome";
        using var cmd = new MySqlCommand(sql, conexao);
        if (saida) cmd.Parameters.AddWithValue("@setor", setor);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            resultado.Add(new ProdutoSelecao(reader.IsDBNull(0) ? "" : reader.GetString(0),
                reader.IsDBNull(1) ? "" : reader.GetString(1)));
        return resultado;
    }

    private void CarregarProdutos_Click(object sender, RoutedEventArgs e)
    {
        if (chkProdutos.IsChecked != true)
        {
            MessageBox.Show("Marque 'Preencher nomes dos produtos cadastrados' primeiro.", "Pousada Jardim");
            return;
        }
        try
        {
            var produtos = ConsultarProdutos(cmbTipo.SelectedIndex == 0, txtSetor.Text.Trim());
            lstProdutos.ItemsSource = produtos;
            lstProdutos.SelectAll();
            AtualizarContagem();
            if (produtos.Count == 0) MessageBox.Show("Nenhum produto encontrado para os filtros informados.", "Pousada Jardim");
        }
        catch (Exception ex) { MessageBox.Show("Falha na consulta: " + ex.Message, "Pousada Jardim"); }
    }

    private void SelecaoAlterada(object sender, RoutedEventArgs e)
    {
        if (lstProdutos == null) return;
        lstProdutos.ItemsSource = null; // Evita usar lista antiga após alterar tipo/filtro.
        AtualizarContagem();
    }
    private void SetorAlterado(object sender, TextChangedEventArgs e)
    {
        if (lstProdutos == null) return;
        lstProdutos.ItemsSource = null;
        AtualizarContagem();
    }
    private void ListaSelecaoAlterada(object sender, SelectionChangedEventArgs e) => AtualizarContagem();
    private void Todos_Click(object sender, RoutedEventArgs e) { lstProdutos.SelectAll(); AtualizarContagem(); }
    private void Nenhum_Click(object sender, RoutedEventArgs e) { lstProdutos.UnselectAll(); AtualizarContagem(); }
    private void AtualizarContagem()
    {
        if (txtContagem == null || lstProdutos == null) return;
        txtContagem.Text = $"{lstProdutos.SelectedItems.Count} selecionado(s) de {lstProdutos.Items.Count} carregado(s).";
    }

    private void GerarPdf_Click(object sender, RoutedEventArgs e)
    {
        bool saida = cmbTipo.SelectedIndex == 0;
        var itens = new List<RequisicaoItem>();
        if (chkProdutos.IsChecked == true)
        {
            if (lstProdutos.Items.Count == 0)
            {
                MessageBox.Show("Carregue os produtos antes de gerar o PDF, ou desmarque a opção para imprimir em branco.", "Pousada Jardim");
                return;
            }
            itens = lstProdutos.SelectedItems.Cast<ProdutoSelecao>()
                .Select(p => new RequisicaoItem(p.Nome, p.Unidade)).ToList();
            if (itens.Count == 0)
            {
                MessageBox.Show("Selecione ao menos um produto ou desmarque a opção para gerar um formulário em branco.", "Pousada Jardim");
                return;
            }
        }
        DateTime data = dpEmissao.SelectedDate ?? DateTime.Today;
        var dialogo = new SaveFileDialog
        {
            Filter = "Documento PDF (*.pdf)|*.pdf",
            FileName = $"Requisicao_{(saida ? "Saida" : "Entrada")}_{data:yyyyMMdd}.pdf"
        };
        if (dialogo.ShowDialog() != true) return;
        try
        {
            RequisicaoFormularioPdf.Gerar(dialogo.FileName, saida,
                data, dpEfetiva.SelectedDate,
                txtSetor.Text, txtSolicitante.Text, txtPeriodo.Text,
                txtFuncionario.Text, txtEntrega.Text, txtLancamento.Text, itens);
            MessageBox.Show("PDF gerado. Nenhum saldo foi alterado.", "Pousada Jardim");
            Process.Start(new ProcessStartInfo(dialogo.FileName) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível gerar o PDF: " + ex.Message, "Pousada Jardim"); }
    }
}
