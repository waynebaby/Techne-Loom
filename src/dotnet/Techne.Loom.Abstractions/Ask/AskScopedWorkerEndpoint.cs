using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Techne.Loom.Common.TaskTracking.Runtime;

namespace Techne.Loom.Common.TaskTracking.Runtime;

public sealed record AskScopedWorkerEndpoint(string AskId, string Url, DateTimeOffset ExpiresAtUtc);
