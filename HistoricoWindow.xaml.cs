using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace CadastroProdutoWpf;

public partial class HistoricoWindow : Window
{
    private readonly HistoricoMovimentacaoDAO dao = new();
    private List<HistoricoMovimentacaoItem> resultados = new();
    public HistoricoWindow()
    {
        InitializeComponent();
        dpInicio.SelectedDate = DateTime.Today.AddDays(-30);
        dpFim.SelectedDate = DateTime.Today;
        Loaded += (_, _) => Consultar();
    }
    private void BtnConsultar_Click(object sender, RoutedEventArgs e) => Consultar();
    private void Consultar()
    {
        if (dpInicio.SelectedDate is not DateTime inicio || dpFim.SelectedDate is not DateTime fim || inicio > fim)
        { MessageBox.Show("Informe um intervalo de datas válido."); return; }
        try
        {
            string? tipo = (cmbTipo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            resultados = dao.Consultar(inicio.Date, fim.Date.AddDays(1), string.IsNullOrEmpty(tipo) ? null : tipo);
            gridHistorico.ItemsSource = resultados;
        }
        catch (Exception ex) { MessageBox.Show("Falha na consulta: " + ex.Message); }
    }
    private static string Csv(string? valor)
    {
        string texto = valor ?? "";
        return "\"" + texto.Replace("\"", "\"\"") + "\"";
    }

    private void BtnExportar_Click(object sender, RoutedEventArgs e)
    {
        if (resultados.Count == 0)
        {
            MessageBox.Show("Consulte movimentações antes de exportar.");
            return;
        }
        var dialogo = new SaveFileDialog
        {
            Title = "Exportar histórico de movimentações",
            Filter = "Arquivo CSV (*.csv)|*.csv",
            FileName = $"historico_pousada_jardim_{DateTime.Today:yyyyMMdd}.csv"
        };
        if (dialogo.ShowDialog(Application.Current.MainWindow) != true) return;
        try
        {
            var cultura = CultureInfo.GetCultureInfo("pt-BR");
            var linhas = new List<string>
            {
                "Código;Data;Tipo;Produto;Origem;Destino;Lote;Quantidade;Responsável"
            };
            foreach (var item in resultados)
                linhas.Add(string.Join(";", new[]
                {
                    item.Codigo.ToString(), Csv(item.Data.ToString("dd/MM/yyyy HH:mm:ss")),
                    Csv(item.Tipo), Csv(item.Produto), Csv(item.Origem), Csv(item.Destino),
                    Csv(item.Lote), Csv(item.Quantidade.ToString("N2", cultura)), Csv(item.Usuario)
                }));
            File.WriteAllLines(dialogo.FileName, linhas, new UTF8Encoding(true));
            MessageBox.Show("Histórico exportado com sucesso.");
        }
        catch (Exception ex) { MessageBox.Show("Falha ao exportar: " + ex.Message); }
    }

    private void BtnPdf_Click(object sender, RoutedEventArgs e)
    {
        if (resultados.Count == 0) { MessageBox.Show("Consulte movimentações antes de gerar o PDF."); return; }
        var dialogo = new SaveFileDialog { Title = "Salvar histórico em PDF", Filter = "Documento PDF (*.pdf)|*.pdf",
            FileName = $"historico_pousada_jardim_{DateTime.Today:yyyyMMdd}.pdf" };
        if (dialogo.ShowDialog(Application.Current.MainWindow) != true) return;
        try
        {
            var cultura = CultureInfo.GetCultureInfo("pt-BR");
            RelatorioPdf.Salvar(dialogo.FileName, "Histórico de movimentações",
                new[] { "Data", "Tipo", "Produto", "Origem", "Destino", "Lote", "Qtd.", "Usuário" },
                resultados.Select(x => new[] { x.Data.ToString("dd/MM/yy"), x.Tipo, x.Produto,
                    x.Origem, x.Destino, x.Lote, x.Quantidade.ToString("N2", cultura), x.Usuario }));
            MessageBox.Show("PDF salvo com sucesso.");
        }
        catch (Exception ex) { MessageBox.Show("Não foi possível gerar o PDF: " + ex.Message); }
    }

}
