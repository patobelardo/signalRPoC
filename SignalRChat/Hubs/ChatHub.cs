using Microsoft.AspNetCore.SignalR;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace SignalRChat.Hubs
{
    public class ChatHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
        public async Task SendPrivateMessage(string user, string destGroup, string message)
        {
            await Clients.Group(destGroup).SendAsync("ReceiveMessage", user, message);
        }


        public async Task AddToGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            await Clients.Group(groupName).SendAsync("ReceiveMessage", "hub", $"{Context.ConnectionId} has joined the group {groupName}.");
        }


        public async Task Login(string userName)
        {
            //Get groups from the notification API OR Redis
            
            string[] groupList = await _getGroupsRegistered(userName); //{"info", "warning"};
            foreach(var groupName in groupList)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                await Clients.Group(groupName).SendAsync("ReceiveMessage", "hub", $"User: {userName} - {Context.ConnectionId} has joined the group {groupName}.");
            }
        }

        private async Task<string[]> _getGroupsRegistered(string userName)
        {
            var notificationAPI = Environment.GetEnvironmentVariable("notificationAPI") ?? "http://localhost:4300";
            Console.WriteLine($"NotificationAPI: {notificationAPI}");
            
            //Use httpclientfactory - this is for demo only
            using(HttpClient httpClient = new HttpClient())
            {
                using (HttpResponseMessage response = await httpClient.GetAsync($"{notificationAPI}/registration?username={userName}"))
                {
                    using (HttpContent content = response.Content)
                    {
                        string contentString = await content.ReadAsStringAsync();
                        
                        return JsonSerializer.Deserialize<string[]>(contentString);
                    }

                }
            }
            
        }
    }
}