using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class SignalRClient
{
    public HubConnection Connection;
    private readonly ILogger<SignalRClient> _logger;
    public SignalRClient(IConfiguration config, ILogger<SignalRClient> logger)
    {
        _logger = logger;
        InitAsync(config["SignalRHub"].ToString()).Wait();
    }

    //Using it just to start the instance at loding time - demo purposes
    public void Initialize()
    {

    }

    private async Task InitAsync(string endpoint){
        Connection = new HubConnectionBuilder()
            .WithUrl(endpoint)
            .Build();

        Connection.Closed += async (error) =>
        {
            await _connect();
        };

        //For demo purposes only
        await _connect();
    }

    private async Task _connect()
    {
        while(true)
        {
            try
            {
                await Connection.StartAsync();
                _logger.LogInformation("Connection started");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                _logger.LogError("Retrying in 1 sec");
                await Task.Delay(2000);
            }
        }    }
}