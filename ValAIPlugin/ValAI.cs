using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections;
using System.Reflection;
using UnityEngine.SceneManagement;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace ValAI
{
    [BepInPlugin("aedenthorn.ValAI", "ValAI", "0.1.0")]
    public class ValAI : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("aedenthorn.ValAI");
        private static ValAI instance;
        private bool welcomed = false;

        // Display do THOR no chat (Rich Text)
        private const string ThorDisplay = "<b>THOR</b>";

        // Tag de cor para mensagens da IA (amarelo)
        private const string AiColorStart = "<color=#FFFF00>";
        private const string AiColorEnd = "</color>";

        // Config para guardar a API key localmente (edite BepInEx/config/ValAI.cfg)
        private ConfigEntry<string> openAiKey;

        void Awake()
        {
            instance = this;
            harmony.PatchAll();
            Logger.LogInfo("ValAI TESTE carregado!");

            // Bind config
            openAiKey = Config.Bind("OpenAI", "ApiKey", "", "OpenAI API Key (não comitar)");

            // Assina evento de cena para detectar quando um mundo/scene é carregado
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Também inicia verificação imediata caso já estejamos na cena correta
            StartCoroutine(WaitForChatAndWelcome());
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Inicia verificação quando uma nova cena é carregada
            StartCoroutine(WaitForChatAndWelcome());
        }

        [HarmonyPatch(typeof(Chat), "InputText")]
        class Chat_InputText_Patch
        {
            static bool Prefix(Chat __instance)
            {
                var m_inputField = __instance.GetType().GetField("m_input", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (m_inputField == null) return true;

                var inputField = m_inputField.GetValue(__instance);
                if (inputField == null) return true;

                var textProperty = inputField.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (textProperty == null) return true;

                string inputText = textProperty.GetValue(inputField) as string;
                                                
                if (!string.IsNullOrEmpty(inputText) && inputText.StartsWith("/ai "))
                {
                    string question = inputText.Substring(4).Trim();

                    if (!string.IsNullOrEmpty(question))
                    {
                        instance.Logger.LogInfo("[ValAI] Comando /ai detectado: " + question);
                        // Chama a coroutine que faz a requisição à OpenAI
                        instance.StartCoroutine(instance.CallOpenAiAndReply(question));
                    }

                    // limpa o campo de texto do input
                    textProperty.SetValue(inputField, "");
                    return false;
                }
                return true;
            }
        }

        // Helper para formatar mensagem da IA em amarelo
        private static string Ai(string text)
        {
            return AiColorStart + (text ?? "") + AiColorEnd;
        }

        // Coroutine que chama a OpenAI e escreve no chat (usa HttpClient e parsing simples para evitar dependências ausentes)
        private IEnumerator CallOpenAiAndReply(string question)
        {
            instance.Logger.LogInfo("[ValAI] Iniciando chamada OpenAI para: " + question);

            string key = openAiKey.Value;
            if (string.IsNullOrEmpty(key))
            {
                Logger.LogWarning("[ValAI] OpenAI API key não está configurada. Edite BepInEx/config/ValAI.cfg");
                AddChatString(Chat.instance, Ai(ThorDisplay + ":  API key não configurada. Verifique o arquivo de configuração."));
                yield break;
            }

            AddChatString(Chat.instance, Ai("📝 Processando: " + question));

            // Monta prompt/contexto específico para Valheim
            string systemPrompt = "Você é THOR, um assistente especialista em Valheim. Responda de forma objetiva, focando no jogo Valheim e fornecendo instruções práticas, dicas e referências ao jogo quando relevante.";

            // Tenta obter resumo do inventário e anexa ao prompt (se encontrado)
            try
            {
                string invSummary = GetInventorySummary();
                if (!string.IsNullOrEmpty(invSummary))
                {
                    systemPrompt += "\nContexto do inventário do jogador: " + invSummary;
                    instance.Logger.LogInfo("[ValAI] Inventário anexado ao prompt: " + invSummary);
                }
            }
            catch (Exception ex)
            {
                instance.Logger.LogWarning("[ValAI] Falha ao obter inventário: " + ex.Message);
            }

            // Monta JSON manualmente (escape básico)
            string JsonEscape(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
            string messagesJson = "[" + "{\"role\":\"system\",\"content\":" + JsonEscape(systemPrompt) + "}," + "{\"role\":\"user\",\"content\":" + JsonEscape(question) + "}" + "]";
            string json = "{\"model\":\"gpt-3.5-turbo\",\"temperature\":0.7,\"messages\":" + messagesJson + "}";

            string respText = null;
            Exception httpEx = null;

            // Executa a requisição em Task para não bloquear a thread Unity
            var task = Task.Run(async () =>
            {
                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(30);
                    http.DefaultRequestHeaders.Clear();
                    http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    {
                        var resp = await http.PostAsync("https://api.openai.com/v1/chat/completions", content);
                        // NOTE: PostAsync overload without content above was a mistake; send content properly:
                        // We'll resend correctly below:
                    }
                }
            });

            // Because of C# limitations above, run proper request:
            var task2 = Task.Run(async () =>
            {
                try
                {
                    using (var http = new HttpClient())
                    {
                        http.Timeout = TimeSpan.FromSeconds(30);
                        http.DefaultRequestHeaders.Clear();
                        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                        using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                        {
                            var resp = await http.PostAsync("https://api.openai.com/v1/chat/completions", content);
                            return await resp.Content.ReadAsStringAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    httpEx = ex;
                    return null;
                }
            });

            while (!task2.IsCompleted)
                yield return null;

            if (httpEx != null)
            {
                Logger.LogError("[ValAI] Erro HTTP OpenAI: " + httpEx.Message);
                AddChatString(Chat.instance, Ai(ThorDisplay + ":  Erro ao chamar OpenAI: " + httpEx.Message));
                yield break;
            }

            respText = task2.Result;
            if (string.IsNullOrEmpty(respText))
            {
                Logger.LogError("[ValAI] Resposta OpenAI vazia");
                AddChatString(Chat.instance, Ai(ThorDisplay + ":  Resposta vazia da OpenAI."));
                yield break;
            }

            // Extrai conteúdo assistant da resposta JSON de forma robusta básica
            string contentText = ExtractContentFromOpenAiResponse(respText);
            if (string.IsNullOrEmpty(contentText))
            {
                Logger.LogError("[ValAI] Falha ao extrair content da OpenAI: " + respText);
                AddChatString(Chat.instance, Ai(ThorDisplay + ":  Não foi possível interpretar a resposta da OpenAI."));
                yield break;
            }

            foreach (var line in SplitLongText(contentText, 300))
            {
                AddChatString(Chat.instance, Ai(ThorDisplay + ":  " + line));
                yield return new WaitForSeconds(0.2f);
            }

            Logger.LogInfo("[ValAI] Resposta OpenAI enviada ao chat.");
        }

        // Utility: quebra textos longos em pedaços menores
        private static string[] SplitLongText(string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text)) return new string[] { "" };
            if (text.Length <= maxLen) return new string[] { text };

            var parts = new System.Collections.Generic.List<string>();
            int i = 0;
            while (i < text.Length)
            {
                int len = Mathf.Min(maxLen, text.Length - i);
                parts.Add(text.Substring(i, len));
                i += len;
            }
            return parts.ToArray();
        }

        // Modifique o método AddChatString para usar 'instance.Logger' ao invés de 'Logger'
        private static void AddChatString(Chat chat, string text)
        {
            if (chat == null) return;
            try
            {
                var t = chat.GetType();
                var method = t.GetMethod("AddString", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(string) }, null);
                if (method != null)
                {
                    method.Invoke(chat, new object[] { text });
                    return;
                }

                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name == "AddString")
                    {
                        var p = m.GetParameters();
                        if (p.Length == 1 && p[0].ParameterType == typeof(string))
                        {
                            m.Invoke(chat, new object[] { text });
                            return;
                        }
                    }
                }

                // fallback: usa overload (title, text) mas passa title vazio para manter mesma fonte
                foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (m.Name == "AddString")
                    {
                        var p = m.GetParameters();
                        if (p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == typeof(string))
                        {
                            m.Invoke(chat, new object[] { string.Empty, text });
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (instance != null)
                    instance.Logger.LogError("[ValAI] Falha ao invocar AddString via reflexão: " + ex.Message);
            }
        }

        // Extrai a primeira ocorrência de choices[0].message.content sem dependências externas
        private static string ExtractContentFromOpenAiResponse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int idxChoices = json.IndexOf("\"choices\"");
            if (idxChoices < 0) idxChoices = 0;
            int idxContent = json.IndexOf("\"content\"", idxChoices);
            if (idxContent < 0) return null;
            int colon = json.IndexOf(':', idxContent);
            if (colon < 0) return null;
            int quoteStart = json.IndexOf('"', colon);
            if (quoteStart < 0) return null;
            int i = quoteStart + 1;
            var sb = new StringBuilder();
            bool escape = false;
            for (; i < json.Length; i++)
            {
                char c = json[i];
                if (escape) { sb.Append(c); escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') break;
                sb.Append(c);
            }
            var result = sb.ToString();
            return result.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"");
        }

        // Novo: tenta obter um resumo do inventário do jogador via reflexão
        private string GetInventorySummary()
        {
            try
            {
                object player = FindPlayerInstance();
                if (player == null)
                {
                    instance.Logger.LogDebug("[ValAI] Player instance não encontrada via reflexão.");
                    return null;
                }

                // procura membro cujo nome contenha "inventory"
                var t = player.GetType();
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (f.Name.ToLower().Contains("inventory"))
                    {
                        var invObj = f.GetValue(player);
                        var s = SummarizeInventory(invObj);
                        if (!string.IsNullOrEmpty(s)) return s;
                    }
                }
                foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (p.Name.ToLower().Contains("inventory"))
                    {
                        var invObj = p.GetValue(player);
                        var s = SummarizeInventory(invObj);
                        if (!string.IsNullOrEmpty(s)) return s;
                    }
                }

                // fallback: se o próprio player for um inventário colecionável
                var fallback = SummarizeInventory(player);
                return fallback;
            }
            catch (Exception ex)
            {
                instance.Logger.LogWarning("[ValAI] Erro GetInventorySummary: " + ex.Message);
                return null;
            }
        }

        private object FindPlayerInstance()
        {
            // tenta localizar um tipo "Player" em assemblies carregados
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var type in types)
                {
                    if (type.Name == "Player")
                    {
                        // tenta campo estático m_localPlayer
                        var fld = type.GetField("m_localPlayer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fld != null)
                        {
                            var val = fld.GetValue(null);
                            if (val != null) return val;
                        }
                        // tenta propriedade estática localPlayer / instance
                        var prop = type.GetProperty("m_localPlayer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                   ?? type.GetProperty("localPlayer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                                   ?? type.GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (prop != null)
                        {
                            var val = prop.GetValue(null);
                            if (val != null) return val;
                        }
                    }
                }
            }

            // se não achou Player, tenta Chat.instance para achar o player via campos conhecidos
            try
            {
                var chat = Chat.instance;
                if (chat != null)
                {
                    var ct = chat.GetType();
                    foreach (var f in ct.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (f.FieldType.Name == "Player")
                        {
                            var val = f.GetValue(chat);
                            if (val != null) return val;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private string SummarizeInventory(object invObj)
        {
            if (invObj == null) return null;

            // tenta encontrar uma coleção enumerável dentro do objeto de inventário
            IEnumerable itemsEnumerable = FindEnumerableInObject(invObj);
            if (itemsEnumerable == null)
            {
                // se invObj já for enumerável
                if (invObj is IEnumerable ie && !(invObj is string))
                    itemsEnumerable = ie;
            }

            if (itemsEnumerable == null)
                return null;

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int seen = 0;
            foreach (var item in itemsEnumerable)
            {
                if (item == null) continue;
                if (seen >= 40) break; // limite de varredura por segurança

                string name = TryGetItemName(item) ?? item.ToString();
                int qty = TryGetItemQuantity(item);

                if (string.IsNullOrEmpty(name)) name = "item";

                if (qty <= 0) qty = 1;

                if (!counts.ContainsKey(name))
                    counts[name] = qty;
                else
                    counts[name] += qty;

                seen++;
            }

            if (counts.Count == 0) return null;

            // constrói resumo limitando o número de entradas
            var parts = new List<string>();
            int added = 0;
            foreach (var kv in counts)
            {
                parts.Add(kv.Value + "x " + kv.Key);
                added++;
                if (added >= 10) break;
            }

            return string.Join(", ", parts);
        }

        private IEnumerable FindEnumerableInObject(object obj)
        {
            if (obj == null) return null;
            var t = obj.GetType();

            // procura campos/propriedades que sejam IEnumerable
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (typeof(IEnumerable).IsAssignableFrom(f.FieldType) && f.FieldType != typeof(string))
                {
                    var val = f.GetValue(obj) as IEnumerable;
                    if (val != null) return val;
                }
            }
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!p.CanRead) continue;
                if (typeof(IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string))
                {
                    try
                    {
                        var val = p.GetValue(obj) as IEnumerable;
                        if (val != null) return val;
                    }
                    catch { }
                }
            }
            return null;
        }

        private string TryGetItemName(object item)
        {
            if (item == null) return null;
            var t = item.GetType();

            // tenta campos/propriedades comuns
            string[] nameCandidates = { "m_shared", "shared", "m_prefab", "m_item", "m_name", "name", "m_dropPrefab" };
            foreach (var cand in nameCandidates)
            {
                var f = t.GetField(cand, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    var val = f.GetValue(item);
                    if (val != null)
                    {
                        // se for objeto com m_name dentro
                        var sub = val.GetType();
                        var subName = sub.GetField("m_name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                    ?? sub.GetField("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (subName != null)
                        {
                            var nm = subName.GetValue(val) as string;
                            if (!string.IsNullOrEmpty(nm)) return nm;
                        }

                        var propName = val.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (propName != null)
                        {
                            var nm = propName.GetValue(val) as string;
                            if (!string.IsNullOrEmpty(nm)) return nm;
                        }

                        var s = val.ToString();
                        if (!string.IsNullOrEmpty(s)) return s;
                    }
                }

                var p = t.GetProperty(cand, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                {
                    try
                    {
                        var val = p.GetValue(item);
                        if (val != null)
                        {
                            var nmProp = val.GetType().GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (nmProp != null)
                            {
                                var nm = nmProp.GetValue(val) as string;
                                if (!string.IsNullOrEmpty(nm)) return nm;
                            }
                            var s = val.ToString();
                            if (!string.IsNullOrEmpty(s)) return s;
                        }
                    }
                    catch { }
                }
            }

            // procura diretamente m_name / name no próprio item
            var fName = t.GetField("m_name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (fName != null)
            {
                var v = fName.GetValue(item) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            var pName = t.GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (pName != null)
            {
                try
                {
                    var v = pName.GetValue(item) as string;
                    if (!string.IsNullOrEmpty(v)) return v;
                }
                catch { }
            }

            return null;
        }

        private int TryGetItemQuantity(object item)
        {
            if (item == null) return 0;
            var t = item.GetType();

            // procura campos/propriedades comuns que contem quantidade
            string[] qtyCandidates = { "m_stack", "m_amount", "m_count", "amount", "count", "stack" };
            foreach (var cand in qtyCandidates)
            {
                var f = t.GetField(cand, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    var val = f.GetValue(item);
                    if (val is int) return (int)val;
                    if (val is long) return (int)(long)val;
                    if (val is short) return (int)(short)val;
                    if (val is byte) return (int)(byte)val;
                }
                var p = t.GetProperty(cand, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null)
                {
                    try
                    {
                        var val = p.GetValue(item);
                        if (val is int) return (int)val;
                        if (val is long) return (int)(long)val;
                        if (val is short) return (int)(short)val;
                        if (val is byte) return (int)(byte)val;
                    }
                    catch { }
                }
            }

            return 0;
        }

        // Espera até que o Chat.instance esteja pronto e envia a mensagem de boas-vindas uma única vez por mundo
        private IEnumerator WaitForChatAndWelcome()
        {
            if (welcomed)
                yield break;

            // Aguarda até que a instância do chat exista
            while (Chat.instance == null)
                yield return null;

            // Pequeno atraso para garantir que o mundo e o UI estejam totalmente prontos
            yield return new WaitForSeconds(1f);

            // Dupla verificação antes de enviar
            if (!welcomed && Chat.instance != null)
            {
                AddChatString(Chat.instance, Ai("Bem vindo Guerreiro! Eu " + ThorDisplay + " estou aqui para ouvir suas preces"));
                instance.Logger.LogInfo("[ValAI] Mensagem de boas-vindas enviada.");
                welcomed = true;
            }
        }
    }
}
