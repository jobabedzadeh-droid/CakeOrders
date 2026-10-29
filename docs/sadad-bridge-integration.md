# SadadBridge configuration API integration

CakeOrders exposes a bearer-protected endpoint for retrieving the saved settings of an active terminal:

`GET https://<cakeorders-host>/api/bridge/terminals/{terminalId}`

Required header:

`Authorization: Bearer <same-value-as-SadadBridge__ApiKey>`

Example JSON response:

```json
{
  "id": 1,
  "name": "صندوق شماره ۱",
  "ipAddress": "192.16.1.119",
  "port": 8888,
  "connectionType": "LAN",
  "terminalCode": null
}
```

The API key is configured on the CakeOrders server as the environment variable `SadadBridge__ApiKey`. Store the same secret locally in the Windows Forms bridge configuration; do not put it in source control. Use HTTPS and restrict access to the internal network.

## .NET Framework 4.8 example

Add a DTO and fetch the configuration before starting a sale. The following example uses APIs available to a .NET Framework 4.8 Windows Forms application:

```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

[DataContract]
public sealed class PosTerminalSettings
{
    [DataMember(Name = "id")] public long Id { get; set; }
    [DataMember(Name = "name")] public string Name { get; set; }
    [DataMember(Name = "ipAddress")] public string IpAddress { get; set; }
    [DataMember(Name = "port")] public int Port { get; set; }
    [DataMember(Name = "connectionType")] public string ConnectionType { get; set; }
    [DataMember(Name = "terminalCode")] public string TerminalCode { get; set; }
}

public static async Task<PosTerminalSettings> FetchTerminalSettingsAsync(
    string apiBaseUrl,
    long terminalId,
    string apiKey)
{
    using (var client = new HttpClient())
    {
        client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        using (var response = await client.GetAsync(
            "api/bridge/terminals/" + terminalId).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();

            using (var stream = await response.Content.ReadAsStreamAsync()
                .ConfigureAwait(false))
            {
                var serializer = new DataContractJsonSerializer(
                    typeof(PosTerminalSettings));
                return (PosTerminalSettings)serializer.ReadObject(stream);
            }
        }
    }
}
```

In the form, call this method before `AsyncSaleTransaction()`; only after successful retrieval set `_pcPos.Ip = settings.IpAddress` and `_pcPos.Port = settings.Port`. Do not allow an operator to override these values in the payment form. Cache the settings only for a short period, and handle API/network failures by refusing to start a new transaction.

This endpoint only retrieves configuration. It does not start a payment and must not be treated as evidence that a transaction succeeded. A separate authenticated callback or polling flow is required before CakeOrders records a payment as successful.
