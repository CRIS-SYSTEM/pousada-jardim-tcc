
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    public partial class ModulosWindow : Window
    {
        private readonly Usuario usuarioLogado;

        public ModulosWindow(Usuario usuario)
        {
            InitializeComponent();

            usuarioLogado = usuario;

            lblUsuario.Text = usuario.Nome;
            lblPerfil.Text = usuario.Perfil;

            // Controle de acesso ao módulo Usuários.
            if (usuario.Perfil != "ADMINISTRADOR")
            {
                btnUsuarios.Visibility = Visibility.Collapsed;
            }

            // Carrega o dashboard ao abrir a janela.
            Loaded += (s, e) => CarregarDashboard();
        }

        private void Inicio_Click(object sender, RoutedEventArgs e)
        {
            ExibirDashboard();
        }

        // =====================================
        // CARREGAR INDICADORES DO DASHBOARD
        // =====================================

        private void CarregarDashboard()
        {
            try
            {
                var estoqueDAO = new EstoqueConsultaDAO();

                using var conexao =
                    ConexaoFactory.CriarConexao();

                conexao.Open();

                // Total de produtos cadastrados.
                const string sqlTotal = @"
                    SELECT COUNT(*)
                    FROM produto";

                using (var comando =
                    new MySqlCommand(sqlTotal, conexao))
                {
                    txtTotalProdutos.Text =
                        Convert.ToInt32(
                            comando.ExecuteScalar()
                        ).ToString();
                }

                // Produtos com maior estoque total.
                const string sqlProdutos = @"
                    SELECT
                        p.id_produto,
                        p.nome,
                        COALESCE(
                            SUM(ps.quantidade_disponivel),
                            0
                        ) AS quantidade
                    FROM produto p
                    LEFT JOIN produto_setor ps
                        ON ps.id_produto = p.id_produto
                    GROUP BY
                        p.id_produto,
                        p.nome
                    ORDER BY
                        quantidade DESC,
                        p.nome ASC
                    LIMIT 5";

                var produtos = new List<ProdutoResumo>();

                using (var comando =
                    new MySqlCommand(sqlProdutos, conexao))
                using (var leitor = comando.ExecuteReader())
                {
                    while (leitor.Read())
                    {
                        produtos.Add(new ProdutoResumo
                        {
                            Codigo =
                                leitor.GetInt32("id_produto"),

                            Nome =
                                leitor.GetString("nome"),

                            Quantidade =
                                leitor.GetDecimal("quantidade")
                        });
                    }
                }

                gridMaiorEstoque.ItemsSource = produtos;

                // Indicadores de estoque mínimo.
                txtAcimaMinimo.Text =
                    estoqueDAO
                        .ContarAcimaDoMinimo()
                        .ToString();

                txtAbaixoMinimo.Text =
                    estoqueDAO
                        .ContarAbaixoDoMinimo()
                        .ToString();

                // Quantidade de movimentações registradas.
                txtMovimentacoes.Text =
                    estoqueDAO
                        .ContarMovimentacoes()
                        .ToString();
            }
            catch (Exception ex)
            {
                txtTotalProdutos.Text = "—";
                txtAcimaMinimo.Text = "—";
                txtAbaixoMinimo.Text = "—";
                txtMovimentacoes.Text = "—";

                gridMaiorEstoque.ItemsSource = null;

                MessageBox.Show(
                    "Não foi possível carregar os indicadores."
                    + "\n\nDetalhes:\n"
                    + ex.Message,
                    "Erro no Dashboard",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // =====================================
        // NAVEGAÇÃO PELOS MÓDULOS
        // =====================================

        // Mantém a instância da janela de origem viva, enquanto seu conteúdo
        // visual é exibido no painel. Não abre novas janelas para os módulos.
        private Window? moduloAtivo;

        public void VoltarAoPainel() => ExibirDashboard();

        private void ExibirDashboard()
        {
            conteudoModulo.Content = null;
            txtTituloPagina.Text = "Visão Geral  •  Painel Principal";
            txtSubtituloPagina.Text = "Indicadores e atalhos do controle de estoque";
            conteudoModulo.Visibility = Visibility.Collapsed;
            painelDashboard.Visibility = Visibility.Visible;
            moduloAtivo = null;
            CarregarDashboard();
        }

        private void Modulo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button botao) return;
            string modulo = botao.Tag?.ToString() ?? "";
            if (modulo == "Usuarios" && usuarioLogado.Perfil != "ADMINISTRADOR")
            {
                MessageBox.Show("Acesso restrito ao administrador.");
                return;
            }

            try
            {
                Window janela = modulo switch
                {
                    "Produtos" => new MainWindow(),
                    "Categorias" => new CategoriasWindow(),
                    "Setores" => new SetoresWindow(),
                    "Estoque" => new EstoqueWindow(),
                    "Movimentacoes" => new MovimentacoesWindow(usuarioLogado),
                    "Relatorios" => new RelatoriosGerenciaisWindow(),
                    "Usuarios" => new UsuariosWindow(usuarioLogado),
                    _ => throw new InvalidOperationException("Módulo ainda não disponível: " + modulo)
                };

                // O conteúdo XAML existente é reaproveitado, com todos os eventos,
                // consultas MySQL, cadastros e validações originais.
                // Removemos o conteúdo da Window para poder hospedá-lo no painel.
                object conteudo = janela.Content;
                janela.Content = null;
                conteudoModulo.Content = null;
                moduloAtivo = janela;
                conteudoModulo.Content = conteudo;
                txtTituloPagina.Text = modulo switch
                {
                    "Produtos" => "Produtos  •  Cadastro e gestão",
                    "Categorias" => "Estoque  •  Categorias",
                    "Setores" => "Estoque  •  Setores",
                    "Estoque" => "Consultar Estoque  •  Geral e por setor",
                    "Movimentacoes" => "Movimentações  •  Entrada, saída e transferência",
                    "Relatorios" => "Relatórios  •  Estoque, mínimos e vencimentos",
                    "Usuarios" => "Administração  •  Usuários e permissões",
                    _ => "Pousada Jardim"
                };
                txtSubtituloPagina.Text = "Pousada Jardim  |  Controle de Estoque Setorizado";
                painelDashboard.Visibility = Visibility.Collapsed;
                conteudoModulo.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Não foi possível abrir o módulo.\n\n" + ex.Message,
                    "Pousada Jardim", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =====================================
        // SAIR DA CONTA
        // =====================================

        private void BtnSair_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoginWindow login = new LoginWindow();

            Application.Current.MainWindow = login;

            login.Show();

            Close();
        }
    }
}
