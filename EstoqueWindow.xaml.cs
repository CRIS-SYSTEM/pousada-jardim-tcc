using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    public partial class EstoqueWindow : Window
    {
        private sealed class SetorFiltro
        {
            public int Id { get; set; }
            public string Nome { get; set; } = "";
        }

        private sealed class LinhaEstoque
        {
            public int IdVinculo { get; set; }
            public int IdSetor { get; set; }
            public string Produto { get; set; } = "";
            public string Setor { get; set; } = "";
            public decimal Saldo { get; set; }
            public decimal? Minimo { get; set; }
            public string Situacao => !Minimo.HasValue ? "Sem limite" :
                Saldo <= Minimo.Value ? "Abaixo ou no mínimo" : "Normal";
        }

        private List<LinhaEstoque> linhas = new();
        private readonly CultureInfo cultura = CultureInfo.GetCultureInfo("pt-BR");

        public EstoqueWindow()
        {
            InitializeComponent();
            ((FrameworkElement)Content).Loaded += (_, _) => Carregar();
        }

        private void Carregar()
        {
            try
            {
                var lista = new List<LinhaEstoque>();
                using var conexao = ConexaoFactory.CriarConexao();
                conexao.Open();
                const string sql = @"SELECT ps.id_produto_setor, ps.id_setor,
                    p.nome AS produto, s.nome AS setor,
                    ps.quantidade_disponivel, ps.estoque_minimo
                    FROM produto_setor ps
                    INNER JOIN produto p ON p.id_produto = ps.id_produto
                    INNER JOIN setor s ON s.id_setor = ps.id_setor
                    ORDER BY p.nome, s.nome";
                using (var cmd = new MySqlCommand(sql, conexao))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        lista.Add(new LinhaEstoque
                        {
                            IdVinculo = reader.GetInt32("id_produto_setor"),
                            IdSetor = reader.GetInt32("id_setor"),
                            Produto = reader.GetString("produto"),
                            Setor = reader.GetString("setor"),
                            Saldo = reader.GetDecimal("quantidade_disponivel"),
                            Minimo = reader.IsDBNull(reader.GetOrdinal("estoque_minimo"))
                                ? null : reader.GetDecimal("estoque_minimo")
                        });
                }
                linhas = lista;
                var setores = new List<SetorFiltro> { new() { Id = 0, Nome = "Todos os setores" } };
                using (var cmd = new MySqlCommand("SELECT id_setor, nome FROM setor ORDER BY nome", conexao))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        setores.Add(new SetorFiltro { Id = reader.GetInt32(0), Nome = reader.GetString(1) });
                int filtro = cmbSetor.SelectedValue is int id ? id : 0;
                cmbSetor.ItemsSource = setores;
                cmbSetor.SelectedValue = setores.Any(x => x.Id == filtro) ? filtro : 0;
                AplicarFiltro();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível consultar o estoque: " + ex.Message);
            }
        }

        private void AplicarFiltro()
        {
            if (gridEstoque == null || cmbSetor == null || txtBusca == null || txtAviso == null) return;
            string termo = txtBusca.Text.Trim();
            int setor = cmbSetor.SelectedValue is int id ? id : 0;
            gridEstoque.ItemsSource = linhas.Where(x =>
                (setor == 0 || x.IdSetor == setor) &&
                (termo.Length == 0 || x.Produto.Contains(termo, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            txtAviso.Text = $"{((System.Collections.ICollection)gridEstoque.ItemsSource).Count} vínculo(s)";
        }

        private void Filtro_Changed(object sender, RoutedEventArgs e) => AplicarFiltro();
        private void Atualizar_Click(object sender, RoutedEventArgs e) => Carregar();

        private void SalvarMinimo_Click(object sender, RoutedEventArgs e)
        {
            if (gridEstoque.SelectedItem is not LinhaEstoque linha)
            {
                MessageBox.Show("Selecione uma linha de produto e setor.");
                return;
            }
            decimal? minimo = null;
            string texto = txtMinimo.Text.Trim();
            if (texto.Length > 0)
            {
                if (!decimal.TryParse(texto, NumberStyles.Number, cultura, out decimal valor) ||
                    valor < 0 || valor > 9999999999.99m || decimal.Round(valor, 2) != valor)
                {
                    MessageBox.Show("Informe um mínimo válido com até duas casas decimais.");
                    return;
                }
                minimo = valor;
            }
            try
            {
                using var conexao = ConexaoFactory.CriarConexao();
                conexao.Open();
                using var cmd = new MySqlCommand(
                    "UPDATE produto_setor SET estoque_minimo = @minimo WHERE id_produto_setor = @id", conexao);
                cmd.Parameters.AddWithValue("@minimo", minimo.HasValue ? minimo.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@id", linha.IdVinculo);
                if (cmd.ExecuteNonQuery() == 0)
                    throw new InvalidOperationException("Nenhuma alteração foi aplicada.");
                Carregar();
                MessageBox.Show("Estoque mínimo atualizado.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível salvar: " + ex.Message);
            }
        }
    }
}
