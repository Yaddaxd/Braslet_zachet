using System.Text.Json;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using System.Diagnostics;
using System.Text;
using Plugin.BLE.Abstractions.EventArgs;
using System.Collections.ObjectModel;

namespace Braslet;

public partial class MainPage : ContentPage
{
    private IBluetoothLE ble;
    private IAdapter adapter;
    private IDevice? connectedDevice;

    // Добавляем список для хранения доступных устройств
    public ObservableCollection<IDevice> AvailableDevices { get; } = new();
    public bool IsScanning { get; set; }

    public MainPage()
    {
        InitializeComponent();
        ble = CrossBluetoothLE.Current;
        adapter = CrossBluetoothLE.Current.Adapter;
        // Привязываем список доступных устройств к List View
        DevicesListView.ItemsSource = AvailableDevices;
        IsScanning = false;
    }

    private async void OnScanButtonClicked(object sender, EventArgs e)
    {
        // Очищаем список доступных устройств при каждом сканировании
        AvailableDevices.Clear();
        if (IsScanning)
            return;

        IsScanning = true;

        try
        {
            // Включаем Bluetooth, если он выключен
            if (!ble.State.Equals(BluetoothState.On))
            {
                await DisplayAlert("Сообщение", "Включите Bluetooth", "OK");
                return;
            }

            // Сканируем доступные устройства
            adapter.DeviceDiscovered += (s, a) =>
            {
                Debug.WriteLine($"Device found: {a.Device.Name}, {a.Device.Id}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!AvailableDevices.Any(d => d.Id == a.Device.Id))
                        AvailableDevices.Add(a.Device);
                });
            };

            await adapter.StartScanningForDevicesAsync();
            // Запускаем таймер для остановки сканирования
            await Task.Delay(10000); // Сканируем 10 секунд
            await adapter.StopScanningForDevicesAsync();

            if (AvailableDevices.Count == 0)
            {
                await DisplayAlert("Сообщение", "Устройства не найдены. Попробуйте снова.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Ошибка при сканировании устройств: {ex.Message}", "OK");
        }
        finally
        {
            IsScanning = false;
        }
    }

    public async void OnConnectButtonClicked(object sender, EventArgs e)
    {
        if (DevicesListView.SelectedItem is not IDevice selectedDevice)
        {
            await DisplayAlert("Сообщение", "Выберите устройство из списка", "OK");
            return;
        }
        if (connectedDevice != null && connectedDevice.Id == selectedDevice.Id)
        {
            await DisplayAlert("Сообщение", "Уже подключено к этому устройству", "OK");
            return;
        }

        try
        {
            connectedDevice = selectedDevice;
            // Подключаемся к устройству.
            await adapter.ConnectToDeviceAsync(connectedDevice);

            // Получаем имя устройства и выводим на экран
            ConnectedDeviceLabel.Text = $"Подключено к: {connectedDevice.Name}";

            // Начинаем прослушивать данные от устройства.
            StartListeningForData();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ошибка", $"Ошибка при подключении: {ex.Message}", "OK");
        }
    }

    private async void StartListeningForData()
    {
        try
        {
            // Получаем сервисы и характеристики
            var services = await connectedDevice.GetServicesAsync();
            var service = services.FirstOrDefault(s => s.Id.ToString().StartsWith("YourServiceId")); // Замените на ID вашего сервиса
            if (service == null)
            {
                await DisplayAlert("Ошибка", $"Service not found", "OK");
                return;
            }
            var characteristic = (await service.GetCharacteristicsAsync()).FirstOrDefault(c => c.Id.ToString().StartsWith("YourCharacteristicId")); // Замените на ID вашей характеристики

            if (characteristic == null)
            {
                await DisplayAlert("Ошибка", $"Characteristic not found", "OK");
                return;
            }
            characteristic.ValueUpdated += Characteristic_ValueUpdated;
            await characteristic.StartUpdatesAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error starting listening for data: {ex.Message}");
            await DisplayAlert("Ошибка", $"Ошибка при прослушивании данных: {ex.Message}", "OK");
        }
    }

    private void Characteristic_ValueUpdated(object? sender, CharacteristicUpdatedEventArgs e)
    {
        if (sender is null)
            return;

        var bytes = e.Characteristic.Value;
        if (bytes != null)
        {
            string jsonString = Encoding.UTF8.GetString(bytes);
            Debug.WriteLine($"Received data: {jsonString}");
            // Обновление UI.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    var data = JsonSerializer.Deserialize<DataModel>(jsonString);

                    if (data != null)
                    {
                        TimeLabel.Text = data.Time ?? "-";
                        HeartRateLabel.Text = data.HeartRate.ToString();
                        StepsLabel.Text = data.Steps.ToString();
                        StatusLabel.Text = data.Status ?? "-";
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error parsing JSON: {ex.Message}");
                    DisplayAlert("Ошибка", $"Ошибка при разборе JSON: {ex.Message}", "OK");
                }
            });
        }
    }
}

public class DataModel
{
    public string? Time { get; set; }
    public int HeartRate { get; set; }
    public int Steps { get; set; }
    public string? Status { get; set; }
}
