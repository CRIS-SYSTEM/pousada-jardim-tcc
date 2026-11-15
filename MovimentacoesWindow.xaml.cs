
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MySqlConnector;

namespace CadastroProdutoWpf
{
    public partial class MovimentacoesWindow : Window
    {
        private readonly Usuario usuarioLogado;
        private readonly MovimentacaoService movimentacaoService = new();

        private static readonly CultureInfo CulturaBR =
            CultureInfo.GetCultureInfo("pt-BR");

        private sealed class ProdutoOpcao
        {
            public int IdProduto { get; set; }
            public string Nome { get; set; } = "";
        }

        private sealed class SetorOpcao
        {
            public int IdSetor { get; set; }
            public string Nome { get; set; } = "";
        }

        private sealed class LoteOpcao
        {
            public int IdLote { get; set; }
            public string Descricao { get; set; } = "";
            public decimal Saldo { get; set; }
        }

        public MovimentacoesWindow(Usuario usuario)
        {
            InitializeComponent();

            usuarioLogado = usuario
                ?? throw new ArgumentNullException(nameof(usuario));

            cmbProduto.DisplayMemberPath = "Nome";
            cmbProduto.SelectedValuePath = "IdProduto";

            cmbSetorOrigem.DisplayMemberPath = "Nome";
            cmbSetorOrigem.SelectedValuePath = "IdSetor";

            cmbSetorDestino.DisplayMemberPath = "Nome";
            cmbSetorDestino.SelectedValuePath = "IdSetor";

            cmbLote.DisplayMemberPath = "Descricao";
            cmbLote.SelectedValuePath = "IdLote";

            cmbProduto.SelectionChanged +=
                CmbProduto_SelectionChanged;

            cmbSetorOrigem.SelectionChanged +=
                SelecaoEstoque_SelectionChanged;

            cmbLote.SelectionChanged +=
                SelecaoEstoque_SelectionChanged;

            cmbTipo.SelectedIndex = 0;

            ((FrameworkElement)Content).Loaded += (s, e) => CarregarDados();
        }

        // =====================================
        // CARREGAR PRODUTOS E SETORES
        // =====================================

        private void CarregarDados()
        {
            try
            {
                CarregarProdutos();
                CarregarSetores();
                AtualizarSaldo();
            }
            catch (Exception ex)
            {
                MostrarErro("Erro ao carregar dados.", ex);
            }
        }

        private void CarregarProdutos()
        {
            var produtos = new List<ProdutoOpcao>();

            using var conexao =
                ConexaoFactory.CriarConexao();

            conexao.Open();

            const string sql = @"
                SELECT id_produto, nome
                FROM produto
                WHERE situacao = 1
                ORDER BY nome";

            using var comando =
                new MySqlCommand(sql, conexao);

            using var leitor = comando.ExecuteReader();

            while (leitor.Read())
            {
                produtos.Add(new ProdutoOpcao
                {
                    IdProduto = leitor.GetInt32("id_produto"),
                    Nome = leitor.GetString("nome")
                });
            }

            cmbProduto.ItemsSource = produtos;
        }

        private void CarregarSetores()
        {
            var setores = new List<SetorOpcao>();

            using var conexao =
                ConexaoFactory.CriarConexao();

            conexao.Open();

            const string sql = @"
                SELECT id_setor, nome
                FROM setor
                ORDER BY nome";

            using var comando =
                new MySqlCommand(sql, conexao);

            using var leitor = comando.ExecuteReader();

            while (leitor.Read())
            {
                setores.Add(new SetorOpcao
                {
                    IdSetor = leitor.GetInt32("id_setor"),
                    Nome = leitor.GetString("nome")
                });
            }

            cmbSetorOrigem.ItemsSource = setores;
            cmbSetorDestino.ItemsSource = setores;
        }

        // =====================================
        // CARREGAR LOTES
        // =====================================

        // Lotes são apresentados por produto e setor. Em saída/transferência,
        // apenas lotes com saldo positivo na origem podem ser selecionados.
        // Em entrada, todos os lotes do produto ficam disponíveis.
        private void CarregarLotes(int idProduto, int? selecionarLote = null)
        {
            int? setor = cmbSetorOrigem.SelectedValue is int id ? id : null;
            string tipo = (cmbTipo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ENTRADA";
            bool entrada = tipo == "ENTRADA";
            var lotes = new List<LoteOpcao>();
            using var conexao = ConexaoFactory.CriarConexao();
            conexao.Open();
            const string sql = @"
                SELECT l.id_lote, l.numero_lote, l.data_vencimento,
                       COALESCE(els.quantidade_disponivel, 0) AS saldo
                FROM lote_produto l
                LEFT JOIN produto_setor ps
                    ON ps.id_produto = l.id_produto AND ps.id_setor = @setor
                LEFT JOIN estoque_lote_setor els
                    ON els.id_produto_setor = ps.id_produto_setor
                   AND els.id_lote = l.id_lote
                WHERE l.id_produto = @produto
                  AND (@entrada = 1 OR (@setor IS NOT NULL AND COALESCE(els.quantidade_disponivel, 0) > 0))
                ORDER BY l.data_vencimento IS NULL, l.data_vencimento, l.id_lote";
            using var cmd = new MySqlCommand(sql, conexao);
            cmd.Parameters.AddWithValue("@produto", idProduto);
            cmd.Parameters.AddWithValue("@setor", (object?)setor ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@entrada", entrada ? 1 : 0);
            using var leitor = cmd.ExecuteReader();
            while (leitor.Read())
            {
                int idLote = leitor.GetInt32("id_lote");
                string numero = leitor.IsDBNull(leitor.GetOrdinal("numero_lote"))
                    ? idLote.ToString("D3") : leitor.GetString("numero_lote");
                string vencimento = leitor.IsDBNull(leitor.GetOrdinal("data_vencimento"))
                    ? "Sem vencimento" : leitor.GetDateTime("data_vencimento").ToString("dd/MM/yyyy");
                decimal saldo = leitor.GetDecimal("saldo");
                lotes.Add(new LoteOpcao
                {
                    IdLote = idLote,
                    Saldo = saldo,
                    Descricao = $"Lote {numero} | {vencimento} | Disponível: {saldo:N2}"
                });
            }
            // A opção sem lote representa estoque próprio, não um lote fictício.
            lotes.Insert(0, new LoteOpcao
            {
                IdLote = 0,
                Descricao = "Sem lote (não se aplica)",
                Saldo = 0
            });
            cmbLote.ItemsSource = lotes;
            if (selecionarLote.HasValue && lotes.Exists(x => x.IdLote == selecionarLote.Value))
                cmbLote.SelectedValue = selecionarLote.Value;
            else if (lotes.Count == 1)
                cmbLote.SelectedIndex = 0;
            AtualizarSaldo();
        }

        private void RecarregarLotesDaSelecao()
        {
            int? anterior = cmbLote.SelectedValue is int id ? id : null;
            if (cmbProduto.SelectedValue is int produto)
                CarregarLotes(produto, anterior);
            else
                cmbLote.ItemsSource = null;
        }

        // =====================================
        // EVENTOS DOS CAMPOS
        // =====================================

        private void CmbProduto_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            try { RecarregarLotesDaSelecao(); }
            catch (Exception ex) { MostrarErro("Erro ao carregar os lotes.", ex); }
        }

        private void SelecaoEstoque_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(sender, cmbSetorOrigem))
            {
                try { RecarregarLotesDaSelecao(); }
                catch (Exception ex) { MostrarErro("Erro ao consultar lotes do setor.", ex); }
            }
            else AtualizarSaldo();
        }

        private void CmbTipo_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (cmbTipo.SelectedItem is not ComboBoxItem item)
                return;

            string tipo = item.Tag?.ToString() ?? "";

            if (painelDestino == null ||
                lblSetorOrigem == null)
                return;

            painelDestino.Visibility =
                tipo == "TRANSFERENCIA"
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            lblSetorOrigem.Text =
                tipo == "TRANSFERENCIA"
                    ? "Setor de origem"
                    : "Setor";

            if (tipo != "TRANSFERENCIA" &&
                cmbSetorDestino != null)
            {
                cmbSetorDestino.SelectedIndex = -1;
            }

            try { RecarregarLotesDaSelecao(); }
            catch (Exception ex) { MostrarErro("Erro ao consultar lotes.", ex); }
        }

        // =====================================
        // CONSULTAR SALDO POR LOTE E SETOR
        // =====================================

        private decimal ConsultarSaldo(
            int idProduto,
            int idSetor,
            int idLote)
        {
            using var conexao =
                ConexaoFactory.CriarConexao();

            conexao.Open();

            string sql = idLote == 0 ? @"
                SELECT ps.quantidade_sem_lote
                FROM produto_setor ps
                WHERE ps.id_produto = @produto AND ps.id_setor = @setor" : @"
                SELECT
                    els.quantidade_disponivel
                FROM estoque_lote_setor els
                INNER JOIN produto_setor ps
                    ON ps.id_produto_setor =
                       els.id_produto_setor
                WHERE ps.id_produto = @produto
                  AND ps.id_setor = @setor
                  AND els.id_lote = @lote";

            using var comando =
                new MySqlCommand(sql, conexao);

            comando.Parameters.AddWithValue(
                "@produto", idProduto);

            comando.Parameters.AddWithValue(
                "@setor", idSetor);

            comando.Parameters.AddWithValue(
                "@lote", idLote);

            object? resultado = comando.ExecuteScalar();

            if (resultado == null ||
                resultado == DBNull.Value)
                return 0m;

            return Convert.ToDecimal(resultado);
        }

        private void AtualizarSaldo()
        {
            if (txtSaldoDisponivel == null ||
                cmbProduto == null ||
                cmbSetorOrigem == null ||
                cmbLote == null)
                return;

            if (cmbProduto.SelectedValue is not int produto ||
                cmbSetorOrigem.SelectedValue is not int setor ||
                cmbLote.SelectedValue is not int lote)
            {
                txtSaldoDisponivel.Text =
                    "Selecione produto, setor e lote";
                return;
            }

            try
            {
                decimal saldo =
                    ConsultarSaldo(produto, setor, lote);

                txtSaldoDisponivel.Text =
                    saldo.ToString("N2", CulturaBR);
            }
            catch (Exception ex)
            {
                txtSaldoDisponivel.Text =
                    "Não foi possível consultar";

                MostrarErro(
                    "Erro ao consultar o saldo.", ex);
            }
        }

        // =====================================
        // CADASTRAR LOTE
        // =====================================

        private void BtnCadastrarLote_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (cmbProduto.SelectedValue is not int idProduto)
            {
                MessageBox.Show(
                    "Selecione um produto primeiro.");
                return;
            }

            string numero =
                txtNovoNumeroLote.Text.Trim();

            DateTime? vencimento =
                dpNovoVencimento.SelectedDate;

            if (numero.Length > 50)
            {
                MessageBox.Show(
                    "O número do lote deve ter até 50 caracteres.");
                return;
            }

            // Evita criar repetidamente um lote
            // sem identificação para o mesmo produto.
            try
            {
                using var conexao =
                    ConexaoFactory.CriarConexao();

                conexao.Open();

                if (numero.Length == 0 &&
                    !vencimento.HasValue)
                {
                    const string sqlExistente = @"
                        SELECT id_lote
                        FROM lote_produto
                        WHERE id_produto = @produto
                          AND numero_lote IS NULL
                          AND data_vencimento IS NULL
                        ORDER BY id_lote
                        LIMIT 1";

                    using var consulta =
                        new MySqlCommand(sqlExistente, conexao);

                    consulta.Parameters.AddWithValue(
                        "@produto", idProduto);

                    object? existente =
                        consulta.ExecuteScalar();

                    if (existente != null &&
                        existente != DBNull.Value)
                    {
                        int idExistente =
                            Convert.ToInt32(existente);

                        CarregarLotes(
                            idProduto, idExistente);

                        MessageBox.Show(
                            "Este produto já possui um lote sem identificação. O lote existente foi selecionado.");
                        return;
                    }
                }

                const string sql = @"
                    INSERT INTO lote_produto
                        (id_produto,
                         numero_lote,
                         data_vencimento)
                    VALUES
                        (@produto,
                         @numero,
                         @vencimento)";

                using var comando =
                    new MySqlCommand(sql, conexao);

                comando.Parameters.AddWithValue(
                    "@produto", idProduto);

                comando.Parameters.AddWithValue(
                    "@numero",
                    numero.Length == 0
                        ? DBNull.Value
                        : numero);

                comando.Parameters.AddWithValue(
                    "@vencimento",
                    vencimento.HasValue
                        ? vencimento.Value.Date
                        : DBNull.Value);

                comando.ExecuteNonQuery();

                int idNovoLote =
                    Convert.ToInt32(comando.LastInsertedId);

                if (numero.Length == 0)
                {
                    using var nomear = new MySqlCommand(
                        "UPDATE lote_produto SET numero_lote = @numero WHERE id_lote = @id AND numero_lote IS NULL",
                        conexao);
                    nomear.Parameters.AddWithValue("@numero", idNovoLote.ToString("D3"));
                    nomear.Parameters.AddWithValue("@id", idNovoLote);
                    nomear.ExecuteNonQuery();
                }

                CarregarLotes(
                    idProduto, idNovoLote);

                txtNovoNumeroLote.Clear();
                dpNovoVencimento.SelectedDate = null;

                AtualizarSaldo();

                MessageBox.Show(
                    "Lote cadastrado com sucesso!",
                    "Pousada Jardim",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MostrarErro(
                    "Erro ao cadastrar o lote.", ex);
            }
        }

        // =====================================
        // BUSCAR VÍNCULO PRODUTO-SETOR
        // =====================================

        private int? BuscarVinculo(
            int idProduto,
            int idSetor)
        {
            using var conexao =
                ConexaoFactory.CriarConexao();

            conexao.Open();

            const string sql = @"
                SELECT id_produto_setor
                FROM produto_setor
                WHERE id_produto = @produto
                  AND id_setor = @setor";

            using var comando =
                new MySqlCommand(sql, conexao);

            comando.Parameters.AddWithValue(
                "@produto", idProduto);

            comando.Parameters.AddWithValue(
                "@setor", idSetor);

            object? resultado =
                comando.ExecuteScalar();

            return resultado == null ||
                   resultado == DBNull.Value
                ? null
                : Convert.ToInt32(resultado);
        }

        // =====================================
        // CRIAR VÍNCULO PARA ENTRADA/DESTINO
        // =====================================

        private int ObterOuCriarVinculo(
            int idProduto,
            int idSetor)
        {
            using var conexao =
                ConexaoFactory.CriarConexao();

            conexao.Open();

            const string sql = @"
                INSERT INTO produto_setor
                    (id_produto, id_setor)
                VALUES
                    (@produto, @setor)
                ON DUPLICATE KEY UPDATE
                    id_produto_setor =
                        LAST_INSERT_ID(id_produto_setor)";

            using var comando =
                new MySqlCommand(sql, conexao);

            comando.Parameters.AddWithValue(
                "@produto", idProduto);

            comando.Parameters.AddWithValue(
                "@setor", idSetor);

            comando.ExecuteNonQuery();

            int idVinculo =
                Convert.ToInt32(comando.LastInsertedId);

            if (idVinculo <= 0)
            {
                throw new InvalidOperationException(
                    "Não foi possível identificar o vínculo produto-setor.");
            }

            return idVinculo;
        }

        // =====================================
        // REGISTRAR MOVIMENTAÇÃO
        // =====================================

        private void BtnRegistrar_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (cmbTipo.SelectedItem is not ComboBoxItem item)
            {
                MessageBox.Show(
                    "Selecione o tipo de movimentação.");
                return;
            }

            if (cmbProduto.SelectedValue is not int idProduto)
            {
                MessageBox.Show("Selecione o produto.");
                return;
            }

            if (cmbSetorOrigem.SelectedValue is not int idSetor)
            {
                MessageBox.Show("Selecione o setor.");
                return;
            }

            if (cmbLote.SelectedValue is not int idLote)
            {
                MessageBox.Show("Selecione um lote ou a opção Sem lote.");
                return;
            }

            if (!decimal.TryParse(
                txtQuantidade.Text,
                NumberStyles.Number,
                CulturaBR,
                out decimal quantidade) ||
                quantidade <= 0 ||
                decimal.Round(quantidade, 2) != quantidade ||
                quantidade > 9999999999.99m)
            {
                MessageBox.Show(
                    "Informe uma quantidade positiva com até duas casas decimais.");
                return;
            }

            string tipoTexto =
                item.Tag?.ToString() ?? "";

            if (!Enum.TryParse(
                tipoTexto,
                out TipoMovimentacao tipo) ||
                !Enum.IsDefined(tipo))
            {
                MessageBox.Show(
                    "Tipo de movimentação inválido.");
                return;
            }

            int? setorDestino = null;

            if (tipo == TipoMovimentacao.TRANSFERENCIA)
            {
                if (cmbSetorDestino.SelectedValue
                    is not int destino)
                {
                    MessageBox.Show(
                        "Selecione o setor de destino.");
                    return;
                }

                if (destino == idSetor)
                {
                    MessageBox.Show(
                        "Origem e destino devem ser diferentes.");
                    return;
                }

                setorDestino = destino;
            }

            btnRegistrar.IsEnabled = false;

            try
            {
                // Para saídas e transferências,
                // verifica primeiro o saldo do lote.
                // O serviço também deve verificar
                // o saldo dentro da transação.

                if (tipo != TipoMovimentacao.ENTRADA)
                {
                    decimal saldo =
                        ConsultarSaldo(
                            idProduto,
                            idSetor,
                            idLote);

                    if (quantidade > saldo)
                    {
                        MessageBox.Show(
                            $"Estoque insuficiente.\n\n" +
                            $"Disponível: {saldo:N2}\n" +
                            $"Solicitado: {quantidade:N2}",
                            "Saldo insuficiente",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);

                        return;
                    }
                }

                int idOrigem;

                if (tipo == TipoMovimentacao.ENTRADA)
                {
                    idOrigem =
                        ObterOuCriarVinculo(
                            idProduto, idSetor);
                }
                else
                {
                    int? vinculo =
                        BuscarVinculo(
                            idProduto, idSetor);

                    if (!vinculo.HasValue)
                    {
                        MessageBox.Show(
                            "O produto não possui estoque nesse setor.");
                        return;
                    }

                    idOrigem = vinculo.Value;
                }

                int? idDestino = null;

                if (setorDestino.HasValue)
                {
                    idDestino =
                        ObterOuCriarVinculo(
                            idProduto,
                            setorDestino.Value);
                }

                var movimentacao = new Movimentacao
                {
                    Tipo = tipo,
                    Quantidade = quantidade,
                    IdProdutoSetor = idOrigem,
                    IdProdutoSetorDestino = idDestino,
                    IdLote = idLote == 0 ? null : idLote,
                    IdUsuario = usuarioLogado.IdUsuario
                };

                // Responsável por atualizar os
                // saldos e registrar o histórico.
                movimentacaoService.Registrar(
                    movimentacao);

                MessageBox.Show(
                    "Movimentação registrada com sucesso!",
                    "Pousada Jardim",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                txtQuantidade.Clear();

                // Mantém as seleções para permitir
                // novas movimentações.
                RecarregarLotesDaSelecao();
            }
            catch (Exception ex)
            {
                MostrarErro(
                    "Erro ao registrar a movimentação.", ex);

                AtualizarSaldo();
            }
            finally
            {
                btnRegistrar.IsEnabled = true;
            }
        }

        // =====================================
        // LIMPAR FORMULÁRIO
        // =====================================

        private void BtnLimpar_Click(
            object sender,
            RoutedEventArgs e)
        {
            cmbTipo.SelectedIndex = 0;
            cmbProduto.SelectedIndex = -1;
            cmbSetorOrigem.SelectedIndex = -1;
            cmbSetorDestino.SelectedIndex = -1;
            cmbLote.SelectedIndex = -1;

            txtQuantidade.Clear();
            txtNovoNumeroLote.Clear();
            dpNovoVencimento.SelectedDate = null;

            AtualizarSaldo();
        }


        private void BtnRequisicoes_Click(
          object sender,
             RoutedEventArgs e)
        {
            var janela = new RequisicoesWindow();

            if (this.IsVisible)
            {
                janela.Owner = this;
            }

            janela.WindowStartupLocation =
                WindowStartupLocation.CenterScreen;

            janela.ShowDialog();
        }

        // =====================================
        // MENSAGENS DE ERRO
        // =====================================

        private static void MostrarErro(
            string mensagem,
            Exception ex)
        {
            MessageBox.Show(
                mensagem +
                "\n\nDetalhes:\n" +
                ex.Message,
                "Pousada Jardim",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
