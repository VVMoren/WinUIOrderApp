using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace markapp.Services
{
    public class NkAuthResult
    {
        public string CsrfToken { get; set; }
        public string SessionId { get; set; }
        public string SessionName { get; set; }
        public Dictionary<string, string> SessionCookies => new() { { SessionName, SessionId } };
    }
}