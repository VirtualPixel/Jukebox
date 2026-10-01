using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using Jukebox.Configuration;

namespace Jukebox.Services
{
    // A door on the local machine, off unless you turn it on. Soundboard software, a stream
    // deck, a script, anything that can make an HTTP request can play a clip through your
    // Semibot's mouth:
    //
    //   POST http://127.0.0.1:8793/play   {"name":"airhorn","gain":1,"pitch":1}
    //   GET  http://127.0.0.1:8793/play?name=airhorn&gain=1&pitch=1
    //   GET  http://127.0.0.1:8793/clips
    //
    // The listener binds 127.0.0.1 only, so nothing outside this machine can reach it. Its
    // callbacks land on a worker thread, which has no business touching Unity, so a request is
    // parked in a queue and played on the game thread on the next frame.
    internal static class Endpoint
    {
        private sealed class Ask
        {
            public string Name = "";
            public float Gain = 1f;
            public float Pitch = 1f;
        }

        private static readonly ConcurrentQueue<Ask> waiting = new ConcurrentQueue<Ask>();
        private static HttpListener? listener;

        public static void Start()
        {
            if (!PluginConfig.Endpoint.Value || listener != null)
            {
                return;
            }
            string prefix = "http://127.0.0.1:" + PluginConfig.EndpointPort.Value + "/";
            HttpListener made = new HttpListener();
            made.Prefixes.Add(prefix);
            try
            {
                made.Start();
            }
            catch (Exception e)
            {
                Jukebox.LogAlways("endpoint: could not listen on " + prefix + ": " + e.Message);
                return;
            }
            listener = made;
            made.BeginGetContext(Arrived, made);
            Jukebox.LogAlways("endpoint: listening on " + prefix + " (POST /play, GET /clips).");
        }

        public static void Stop()
        {
            HttpListener? going = listener;
            listener = null;
            if (going != null)
            {
                going.Close();
            }
        }

        private static void Arrived(IAsyncResult result)
        {
            HttpListener made = (HttpListener)result.AsyncState;
            HttpListenerContext context;
            try
            {
                context = made.EndGetContext(result);
            }
            catch (Exception)
            {
                // Closed under us on the way out, which is the only way this ends.
                return;
            }
            try
            {
                made.BeginGetContext(Arrived, made);
            }
            catch (Exception)
            {
                return;
            }
            try
            {
                Answer(context);
            }
            catch (Exception e) when (e is IOException || e is HttpListenerException || e is ObjectDisposedException)
            {
                // The caller hung up halfway through the request. Nothing to answer.
            }
        }

        private static void Answer(HttpListenerContext context)
        {
            string route = context.Request.Url != null ? context.Request.Url.AbsolutePath.TrimEnd('/') : "";
            if (route.Length == 0)
            {
                route = "/";
            }
            if (string.Equals(route, "/clips", StringComparison.OrdinalIgnoreCase))
            {
                StringBuilder json = new StringBuilder("{\"clips\":[");
                string[] names = Library.Names();
                for (int i = 0; i < names.Length; i++)
                {
                    if (i > 0)
                    {
                        json.Append(',');
                    }
                    json.Append('"').Append(Escape(names[i])).Append('"');
                }
                json.Append("]}");
                Send(context, 200, json.ToString());
                return;
            }
            if (!string.Equals(route, "/play", StringComparison.OrdinalIgnoreCase))
            {
                Send(context, 404, "{\"error\":\"no such route\"}");
                return;
            }
            Ask ask = new Ask();
            ask.Name = Query(context.Request.Url?.Query ?? "", "name");
            ask.Gain = Number(Query(context.Request.Url?.Query ?? "", "gain"), 1f);
            ask.Pitch = Number(Query(context.Request.Url?.Query ?? "", "pitch"), 1f);
            if (context.Request.HasEntityBody)
            {
                string body;
                using (StreamReader reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                {
                    body = reader.ReadToEnd();
                }
                string name = Field(body, "name");
                if (name.Length > 0)
                {
                    ask.Name = name;
                }
                ask.Gain = Number(Field(body, "gain"), ask.Gain);
                ask.Pitch = Number(Field(body, "pitch"), ask.Pitch);
            }
            if (ask.Name.Length == 0)
            {
                Send(context, 400, "{\"error\":\"no name\"}");
                return;
            }
            waiting.Enqueue(ask);
            Send(context, 200, "{\"ok\":true,\"name\":\"" + Escape(ask.Name) + "\"}");
        }

        private static void Send(HttpListenerContext context, int status, string body)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Close();
            }
            catch (Exception)
            {
                // The caller hung up. A stream deck does that on every press.
            }
        }

        // One value out of a JSON object, string or number, without a JSON library: the field
        // name, a colon, then either a quoted run or a bare number.
        private static string Field(string body, string field)
        {
            int at = body.IndexOf("\"" + field + "\"", StringComparison.OrdinalIgnoreCase);
            if (at < 0)
            {
                return "";
            }
            int colon = body.IndexOf(':', at);
            if (colon < 0)
            {
                return "";
            }
            int i = colon + 1;
            while (i < body.Length && char.IsWhiteSpace(body[i]))
            {
                i++;
            }
            if (i >= body.Length)
            {
                return "";
            }
            if (body[i] == '"')
            {
                int end = body.IndexOf('"', i + 1);
                return end > i ? body.Substring(i + 1, end - i - 1) : "";
            }
            int stop = i;
            while (stop < body.Length && (char.IsDigit(body[stop]) || body[stop] == '.' || body[stop] == '-' || body[stop] == '+'))
            {
                stop++;
            }
            return body.Substring(i, stop - i);
        }

        private static string Query(string query, string field)
        {
            foreach (string pair in query.TrimStart('?').Split('&'))
            {
                int split = pair.IndexOf('=');
                if (split > 0 && string.Equals(pair.Substring(0, split), field, StringComparison.OrdinalIgnoreCase))
                {
                    return Uri.UnescapeDataString(pair.Substring(split + 1));
                }
            }
            return "";
        }

        private static float Number(string text, float fallback)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");

        // On the game thread, where Unity can be touched.
        public static void Tick()
        {
            while (waiting.TryDequeue(out Ask ask))
            {
                Api.Play(ask.Name, ask.Gain * PluginConfig.SoundboardVolume.Value, ask.Pitch);
            }
        }
    }
}
