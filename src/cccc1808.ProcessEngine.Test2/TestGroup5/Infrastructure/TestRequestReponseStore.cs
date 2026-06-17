using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure
{
    /// <summary>
    /// Тестовая имитация связки request - response;
    /// </summary>
    internal class TestRequestReponseStore
    {
        private readonly Dictionary<string, (JsonElement Request, JsonElement? Response)> _rpc
            = new Dictionary<string, (JsonElement Request, JsonElement? Response)>();

        public void SendRequest(string key, JsonElement request)
        {
            _rpc.Add(key, (request, null));
        }

        public void ReceiveReponse(string key, JsonElement response)
        {
            var request = _rpc[key];
            _rpc[key] = (request.Request, response);
        }

        public bool ResponseReceived(string key, out JsonElement response)
        {
            if (!_rpc.TryGetValue(key, out var request))
            {
                throw new Exception($"Запрос не найден {key}.");
            }

            if (request.Response.HasValue)
            {
                response = request.Response.Value;
                return true;
            }

            response = default;
            return false;
        }
    }
}
