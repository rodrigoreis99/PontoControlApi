using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
// Usings para a nova biblioteca de notificação
using Notifications.Wpf.Core;

namespace PontoControl.App
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _apiClient;
        private readonly DispatcherTimer _timer;
        private const string ApiBaseUrl = "http://localhost:5212";

        // Variáveis de controle de notificação
        private bool _notificacao10MinEnviada = false;
        private bool _notificacaoSaidaEnviada = false;
        private bool _notificacaoVoltaAlmocoEnviada = false;

        // Objeto gerenciador de notificações da nova biblioteca
        private readonly NotificationManager _notificationManager = new NotificationManager();

        public MainWindow()
        {
            InitializeComponent();
            _apiClient = new HttpClient { BaseAddress = new Uri(ApiBaseUrl) };
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _timer.Tick += Timer_Tick;
            this.Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await AtualizarStatusAsync();
            _timer.Start();
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            await AtualizarStatusAsync();
        }

        // ***** MÉTODO DE NOTIFICAÇÃO ATUALIZADO *****
        private async Task MostrarNotificacao(string titulo, string mensagem)
        {
            var notificationContent = new NotificationContent
            {
                Title = titulo,
                Message = mensagem,
                Type = NotificationType.Information // Pode ser Success, Warning, Error, etc.
            };

            // Usando o gerenciador para mostrar a notificação na thread principal da UI
            await _notificationManager.ShowAsync(notificationContent);
        }

        private async Task AtualizarStatusAsync()
        {
            try
            {
                var status = await _apiClient.GetFromJsonAsync<JornadaStatusDto>("/api/jornada/status");
                if (status != null)
                {
                    TempoTrabalhadoTextBlock.Text = status.TempoTrabalhadoHoje;
                    SaidaPrevistaTextBlock.Text = status.HoraSaidaPrevista?.ToString("HH:mm:ss") ?? "--:--";
                    MarcacoesListView.ItemsSource = status.Marcacoes;

                    var agora = DateTime.Now;

                    // Lógica para resetar os alertas para o próximo dia
                    if (status.StatusAtual == StatusJornada.Finalizada || (status.HoraSaidaPrevista.HasValue && agora.Date > status.HoraSaidaPrevista.Value.Date))
                    {
                        _notificacao10MinEnviada = false;
                        _notificacaoSaidaEnviada = false;
                        _notificacaoVoltaAlmocoEnviada = false;
                    }

                    // === NOVO ALERTA: VOLTA DO ALMOÇO ===
                    if (status.InicioAlmoco.HasValue && status.StatusAtual == StatusJornada.EmPausa && !_notificacaoVoltaAlmocoEnviada)
                    {
                        var horaDeVoltar = status.InicioAlmoco.Value.AddHours(1);
                        if (agora >= horaDeVoltar)
                        {
                            await MostrarNotificacao("Hora de Voltar!", "Já se passou 1 hora desde sua saída para o almoço. Não se esqueça de registrar o ponto.");
                            _notificacaoVoltaAlmocoEnviada = true;
                        }
                    }
                    // === FIM DO NOVO ALERTA ===

                    // Alertas de Fim de Expediente
                    if (status.HoraSaidaPrevista.HasValue)
                    {
                        var horaSaida = status.HoraSaidaPrevista.Value;

                        if (!_notificacao10MinEnviada && agora >= horaSaida.AddMinutes(-10) && agora < horaSaida)
                        {
                            await MostrarNotificacao("Controle de Ponto", "Sua jornada de trabalho termina em 10 minutos!");
                            _notificacao10MinEnviada = true;
                        }

                        if (!_notificacaoSaidaEnviada && agora >= horaSaida)
                        {
                            await MostrarNotificacao("Jornada Completa!", "Você já cumpriu suas 8h48m de hoje. Bom descanso!");
                            _notificacaoSaidaEnviada = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TempoTrabalhadoTextBlock.Text = "API offline";
                SaidaPrevistaTextBlock.Text = ex.Message;
            }
        }

        private async void MarcarPontoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MarcarPontoButton.IsEnabled = false;
                MarcarPontoButton.Content = "Marcando...";
                await _apiClient.PostAsync("/api/jornada/marcar", null);
                await AtualizarStatusAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao marcar o ponto: {ex.Message}", "Erro de API", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MarcarPontoButton.IsEnabled = true;
                MarcarPontoButton.Content = "Marcar Ponto";
            }
        }
    }
}