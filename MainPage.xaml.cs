using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace MulticastSocket
{
    public partial class MainPage : ContentPage
    {
        private UdpClient udpClient;
        private Task listenTask;
        private CancellationTokenSource cancellationTokenSource;

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnListen(object sender, EventArgs e)
        {
            // Cancel and clean up any existing tasks and sockets
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                udpClient?.Close();
                await listenTask;
                listenTask = null;
                cancellationTokenSource = null;
            }

            string multicastGroup = group.Text;
            if (!int.TryParse(port.Text, out int multicastPort))
            {
                UpdateConsole("Escriba un grupo y puerto correctos");
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();
            listenTask = ListenMulticast(multicastGroup, multicastPort, cancellationTokenSource.Token);
            await listenTask;
        }

        private async Task ListenMulticast(string multicastGroup, int multicastPort, CancellationToken cancellationToken)
        {
            try
            {
                udpClient = new UdpClient();
                udpClient.ExclusiveAddressUse = false;
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, multicastPort));
                udpClient.JoinMulticastGroup(IPAddress.Parse(multicastGroup));

                UpdateConsole($"Unido al grupo multicast {multicastGroup} en el puerto {multicastPort}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    UpdateConsole("Esperando datos...");
                    var receiveTask = udpClient.ReceiveAsync();
                    var completedTask = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, cancellationToken));

                    if (completedTask == receiveTask)
                    {
                        var result = await receiveTask;
                        string operacion = Encoding.UTF8.GetString(result.Buffer).Trim();

                        if (string.IsNullOrEmpty(url.Text))
                        {
                            UpdateConsole("Datos recibidos");
                            UpdateConsole("Operación recibida: " + operacion);
                        }
                        else
                        {
                            await ProcessWithApi(operacion);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                UpdateConsole("Conexión cancelada.");
            }
            catch (Exception ex)
            {
                UpdateConsole("Error en el socket multicast: " + ex.Message);
            }
            finally
            {
                if (udpClient != null)
                {
                    try
                    {
                        udpClient.DropMulticastGroup(IPAddress.Parse(multicastGroup));
                        udpClient.Close();
                    }
                    catch (Exception ex)
                    {
                        UpdateConsole("Error al cerrar el socket multicast: " + ex.Message);
                    }
                    finally
                    {
                        udpClient = null;
                    }
                }
            }
        }

        private async Task ProcessWithApi(string operacion)
        {
            try
            {
                operacion = WebUtility.UrlEncode(operacion);
                string apiUrl = $"{url.Text}{operacion}";
                UpdateConsole($"Procesando en {apiUrl}");

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        UpdateConsole("Result: " + content);
                    }
                    else
                    {
                        UpdateConsole("Error en la respuesta del servidor: " + response.ReasonPhrase);
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateConsole("Error en la conexión HTTP: " + ex.Message);
            }
        }

        private void UpdateConsole(string message)
        {
            Device.BeginInvokeOnMainThread(() =>
            {
                console.Text += message + "\n";
            });
        }

        private void OnClear(object sender, EventArgs e)
        {
            // Cancel and clean up any existing tasks and sockets
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel();
                udpClient?.Close();
                listenTask = null;
                cancellationTokenSource = null;
            }

            // Clear the console
            Device.BeginInvokeOnMainThread(() =>
            {
                console.Text = string.Empty;
            });
        }
    }
}
