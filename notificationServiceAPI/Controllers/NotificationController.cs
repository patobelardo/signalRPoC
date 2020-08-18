using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using System.Text;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Transactions;

namespace notificationServiceAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly ILogger<NotificationController> _logger;
        private SignalRClient _notificationClient;
        private readonly IDistributedCache _distributedCache;
        private readonly IServer _redisServer;
        private readonly SqlConnection _dbconnection;
        public NotificationController(
            ILogger<NotificationController> logger, 
            SignalRClient notificationClient,
            IDistributedCache distributedCache, 
            IConfiguration config, 
            IDbConnection connection)
        {
            _logger = logger;
            _notificationClient = notificationClient;
            _distributedCache = distributedCache;
            var redis = ConnectionMultiplexer.Connect(config["redisEndpoint"]);
            _redisServer = redis.GetServer(config["redisEndpoint"]);
            _dbconnection = (SqlConnection)connection;
            _dbconnection.Open();
        }

    #region User Registrations (SQL)
        
        [HttpPost("/registration")]
        public void UpdateRegistration([FromBody]Registration registration)
        {
            using (TransactionScope scope = new TransactionScope())
            {
                using (SqlCommand cmd = new SqlCommand(
                    $"DELETE FROM Registration WHERE UserName = '{registration.username}'",
                    _dbconnection))
                {
                    cmd.ExecuteNonQuery();
                }

                foreach(var group in registration.groupList)
                {
                    using (SqlCommand cmd = new SqlCommand(
                        $"INSERT INTO Registration (UserName, GroupName) VALUES ('{registration.username}', '{group}')",
                        _dbconnection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }


        [HttpGet("/registration")]
        public async Task<List<string>> GetRegistrations(string username)
        {
            var items = new List<string>();
            
            using (SqlCommand cmd = new SqlCommand(
                $"SELECT GroupName FROM Registration WHERE UserName = '{username}'",
                _dbconnection))
            {
                using (SqlDataReader dr  = await cmd.ExecuteReaderAsync())
                {
                    while (dr.Read())
                    {
                        items.Add(dr["GroupName"].ToString());
                    }
                }
            }

            return items;
        }

        private async Task<List<string>> getUsersRegisteredAsync(string groupName)
        {
            var items = new List<string>();
            
            using (SqlCommand cmd = new SqlCommand(
                $"SELECT UserName FROM Registration WHERE GroupName = '{groupName}'",
                _dbconnection))
            {
                using (SqlDataReader dr  = await cmd.ExecuteReaderAsync())
                {
                    while (dr.Read())
                    {
                        items.Add(dr["UserName"].ToString());
                    }
                }
            }

            return items;     
       }

    #endregion

    #region Notifications (Redis)

        private async Task<EventDefinition> _getEventAsync(Guid key)
        {
            string cacheKey = $"event:${key}";
            var data = await _distributedCache.GetAsync(cacheKey);
            return JsonSerializer.Deserialize<EventDefinition>(Encoding.UTF8.GetString(data));
        }

        [HttpPost("/event")]
        public async Task<string> NewEvent([FromBody]EventDefinition eventDef)
        {
            //Saving the event information (optional)
            eventDef.Id = Guid.NewGuid();
            //Save at Cache (Redis)
            string cacheKey = $"event:${eventDef.Id}";
            var content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(eventDef));
            await _distributedCache.SetAsync(cacheKey, content);    


            //Checking groups subscribed to that event (membership?)
            //Mocked for now
            string[] destGroupNames; 
            switch(eventDef.Type.ToLower())
            {
                case "info":
                    destGroupNames = new string[] {"info"};
                    break;
                case "warning":
                    destGroupNames = new string[] {"info", "warning"};
                    break;
                case "error":
                    destGroupNames = new string[] {"info", "warning", "error"};
                    break;
                default:
                    destGroupNames = new string[0]; 
                    break;
            };

            //Sending notifications to all groups.
            foreach(var destGroup in destGroupNames)
            {
                await _sendNotification(eventDef, destGroup);
            }

            //Saving state per eventid-group-users/connectionid
            // string cacheKey = $"notif:{username}:{eventid}:{status};
            foreach(var group in destGroupNames)
            {
                var userList = await getUsersRegisteredAsync(group);

                // string value = JsonSerializer.Serialize(eventDef);
                foreach(var user in userList)
                {
                    string keyName = $"notifications:{user}";
                    await _addOrUpdateNotificationAsync(keyName, eventDef.Id);
                }
            }

            return "ok";
        }

        [HttpGet("/notification")]
        public async Task<List<EventDefinition>> GetNotifications(string username, bool read = false, int maxitems = 50)
        {
            string keyName = $"notifications:{username}";
            
            List<EventDefinition> events = new List<EventDefinition>();
            var data = await _distributedCache.GetAsync(keyName);
            if (data != null)
            {
                var list = JsonSerializer.Deserialize<List<KeyValuePair<Guid, bool>>>(Encoding.UTF8.GetString(data));
                // notifications = list.ToDictionary((x,y) => x,y);
                var notifications = list.Where(not => not.Value == read).Take(maxitems);
                foreach(var n in notifications)
                {
                    events.Add(await _getEventAsync(n.Key));
                }
            }

            return events;
        }



        [HttpPost("/notification")]
        public async Task<string> UpdateNotifications(string username, Guid eventID)
        {
            //TODO: for demo purposes only
            string keyName = $"notifications:{username}";
            await _addOrUpdateNotificationAsync(keyName, eventID, true);

            return "done";
        }

        private async Task _addOrUpdateNotificationAsync(string keyName, Guid id, bool read = false)
        {
            Dictionary<Guid, bool> notifications;
            var data = await _distributedCache.GetAsync(keyName);
            if (data != null)
            {
                var list = JsonSerializer.Deserialize<List<KeyValuePair<Guid, bool>>>(Encoding.UTF8.GetString(data));
                // notifications = list.ToDictionary((x,y) => x,y);
                notifications = list.ToDictionary(x => x.Key, x => x.Value);
                // notifications = JsonSerializer.Deserialize<Dictionary<Guid, bool>>(Encoding.UTF8.GetString(data));
            }
            else
            {
                notifications = new Dictionary<Guid, bool>();
            }
            
            if (!notifications.ContainsKey(id))
                notifications.Add(id, false);
            else
                notifications[id]= read;

            var toCache = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(notifications.ToList()));
            await _distributedCache.SetAsync(keyName, toCache);
        }

        #endregion
        private async Task _sendNotification(EventDefinition eventDef, string destination)
        {
            string user = "NotificationAPI";
            string msg = eventDef.Description;
            try
            {
                await _notificationClient.Connection.InvokeAsync(
                    "SendPrivateMessage", 
                    user,
                    destination, 
                    $"[to {destination}] - {JsonSerializer.Serialize(eventDef)}");
            }
            catch (Exception ex)
            {                
                _logger.LogError(ex.ToString());
            }
        }
    }
}
