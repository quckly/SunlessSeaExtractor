using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SunlessSeaExtractor
{
    class QTGrammarCheck
    {
        static char[] end_punct_chars = new char[] { '.', '!', '?', '…', '>', ')', '…' };

        public static void CheckLines(string file, int linenumber, string line, string line_orig, TextWriter writer)
        {
            string tr_clear_line = string.Empty;

            if (line.Count(x => x == '{') != line.Count(x => x == '}'))
                writer.WriteLine("{0} {1} - !!! Count of '{{}}' error.", file, linenumber);

            if (line.Count(x => x == '[') != line.Count(x => x == ']'))
                writer.WriteLine("{0} {1} - !!! Count of '[]' error.", file, linenumber);

            if (line.Contains('–'))
                writer.WriteLine("{0} {1} - !!! Contains '–'", file, linenumber);

            //if (line.Contains('…'))
            //    writer.WriteLine("{0} {1} - contains '…'", file, linenumber);

            // Граматика
            tr_clear_line = line; // Очистить строку от комментов

            if (tr_clear_line.Contains("  "))
                writer.WriteLine("{0} {1} - Два пробела.", file, linenumber);

            if (tr_clear_line != "")
            {
                if (end_punct_chars.Contains(tr_clear_line.Last()) == false && end_punct_chars.Contains(line_orig.LastOrDefault()) == true)
                    writer.WriteLine("{0} {1} - Нет знака в конце строки.", file, linenumber);

                for (int i = 0; i < tr_clear_line.Length; i++)
                {
                    if (tr_clear_line[i] == ',')
                    {
                        if (i != 0 && !char.IsLetter(tr_clear_line[i - 1]) && tr_clear_line[i - 1] != '"' && tr_clear_line[i - 1] != '\'' && tr_clear_line[i - 1] != ')' && !char.IsDigit(tr_clear_line[i - 1]))
                            writer.WriteLine("{0} {1} - Перед запятой не буква.", file, linenumber);

                        if (i != tr_clear_line.Length - 1 && tr_clear_line[i + 1] != ' ' && !char.IsDigit(tr_clear_line[i + 1]))
                            writer.WriteLine("{0} {1} - После запятой нет пробела.", file, linenumber);
                    }
                }
            }
        }
    }

    class SunlessSeaExtractor
    {
        Dictionary<string, string> jsonPropertyNames = new Dictionary<string, string>()
        {
            {"Name", "N"},
            {"Description", "D"},
            {"Tooltip", "T"}
        };

        private TextWriter testOutput;
        private string currentFile;

        private void TraverseJsonSRT(JToken node, List<TextCollector.TXT_collection> txt, ref int traverseTreeIndex, Dictionary<int, string> translationMap, bool extract)
        {
            ++traverseTreeIndex;

            switch (node.Type)
            {
                case JTokenType.Property:
                    {
                        JProperty property = node as JProperty;

                        if (property.Value.Type == JTokenType.String)
                        {
                            if (extract)
                            {
                                string serializeName;
                                if (jsonPropertyNames.TryGetValue(property.Name, out serializeName))
                                {
                                    string txtstr = property.Value.ToString();

                                    if (txtstr == "")
                                    {
                                        //txtstr = "##EMPTY##";
                                        return; // Skip empty lines
                                    }

                                    txtstr = txtstr.Replace("\r", "\\r");
                                    txtstr = txtstr.Replace("\n", "\\n");

                                    txt.Add(new TextCollector.TXT_collection(traverseTreeIndex, serializeName, txtstr, false));
                                }
                            }
                            else
                            {
                                if (translationMap.ContainsKey(traverseTreeIndex))
                                {
                                    if (true)
                                    {
                                        QTGrammarCheck.CheckLines(currentFile, traverseTreeIndex, translationMap[traverseTreeIndex], (property.Value as JValue).Value.ToString(), testOutput);
                                    }

                                    (property.Value as JValue).Value = translationMap[traverseTreeIndex];
                                }
                            }
                        }
                        else
                        {
                            // Traverse value of property
                            TraverseJsonSRT(property.Value, txt, ref traverseTreeIndex, translationMap, extract);
                        }

                        break;
                    }

                case JTokenType.Object:
                case JTokenType.Array:
                    {
                        foreach (var it in node)
                        {
                            TraverseJsonSRT(it, txt, ref traverseTreeIndex, translationMap, extract);
                        }

                        break;
                    }
                default:
                    {
                        return;
                    }
            }
        }

        public void SrtToJson(StreamReader sr, StreamReader trsr, StreamWriter sw, StreamWriter testlog, string fileName)
        {
            sw.Write(SrtToJson(sr.ReadToEnd(), trsr, testlog, fileName));
        }

        public string SrtToJson(string json, StreamReader translated, StreamWriter testlog, string fileName)
        {
            // for test
            testOutput = testlog;
            currentFile = fileName;

            //

            JToken data = JsonConvert.DeserializeObject(json) as JToken;

            // Generate translate dictionary
            Dictionary<int, string> trMap = new Dictionary<int, string>();

            // .SRT format parse algorithm
            string PushString = null;

            while (!translated.EndOfStream)
            {
                int num = -100;

                if (PushString != null)
                {
                    num = Int32.Parse(PushString);
                    PushString = null;
                }
                else
                {
                    var line = translated.ReadLine();
                    if (!line.StartsWith("##COMMENT##"))
                        num = Int32.Parse(line);
                }

                string name = translated.ReadLine();
                string text = translated.ReadLine();
                string empty_string = translated.ReadLine();        // Empty \r\n

                if (/*text == "" &&*/ empty_string.Trim() != "" && empty_string.Trim() != "##COMMENT##")   // If empty line in file lines droped
                {
                    PushString = empty_string;
                }

                var sbTempString = new StringBuilder(text);
                //sbTempString = sbTempString.Replace("|", "\r\n");
                sbTempString = sbTempString.Replace("\\r", "\r");
                sbTempString = sbTempString.Replace("\\n", "\n");

                text = sbTempString.ToString();

                if (text == "##EMPTY##")
                    text = "";
                else if (text == "##SPACE##")
                    text = " ";

                if (text.StartsWith("##COMMENT##"))
                    continue;

                trMap.Add(num, text);
            }

            // Traverse tree and replace strings
            int traverseTreeIndex = 0;
            TraverseJsonSRT(data, null, ref traverseTreeIndex, trMap, false);

            // Return serialized json from object
            return JsonConvert.SerializeObject(data, Formatting.Indented);
        }

        public void JsonToSrt(StreamReader sr, StreamWriter sw)
        {
            sw.Write(JsonToSrt(sr.ReadToEnd()));
        }

        public string JsonToSrt(string json)
        {
            StringBuilder sb = new StringBuilder();

            JToken data = JsonConvert.DeserializeObject(json) as JToken;

            // Create TXT_collection
            var txt = new List<TextCollector.TXT_collection>();
            var txt_export = new List<TextCollector.TXT_collection>();

            int traverseTreeIndex = 0;
            TraverseJsonSRT(data, txt, ref traverseTreeIndex, null, true);

            TextCollector.CreateExportingTXTfromOneFile(txt, ref txt_export);

            // Serialize to SRT
            foreach (var record in txt_export)
            {
                sb.AppendLine(record.number.ToString());
                sb.AppendLine(record.name);
                sb.AppendLine(record.text);
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }

    // From TTG Tools
    class TextCollector
    {
        public class TXT_collection
        {
            public int number;
            public string name;
            public string text;
            public bool exported;

            public TXT_collection()
            {
            }

            public TXT_collection(int _number, string _name, string _text, bool _exported)
            {
                this.name = _name;
                this.number = _number;
                this.text = _text;
                this.exported = _exported;
            }
        }

        public static List<TextCollector.TXT_collection> CreateExportingTXTfromOneFile(List<TextCollector.TXT_collection> txt, ref List<TextCollector.TXT_collection> txt_export)
        {
            // for fast comprassions
            List<Tuple<int, string>> clearTxt = new List<Tuple<int, string>>();

            foreach (var it in txt)
            {
                var str = DeleteCommentsAndOther(it.text);
                clearTxt.Add(new Tuple<int, string>(str.GetHashCode(), str));
            }

            txt_export.Clear();

            for (int index = 0; index < txt.Count; ++index)
            {
                if (txt[index].text == null)
                    txt[index].exported = true;
            }

            for (int index1 = 0; index1 < txt.Count; ++index1) // N^2 / 2 - complexity
            {
                if (!txt[index1].exported)
                {
                    txt_export.Add(txt[index1]);
                    txt[index1].exported = true;
                    for (int index2 = index1 + 1; index2 < txt.Count; ++index2)
                    {
                        //if (!txt[index2].exported && TextCollector.IsStringsSame(txt[index1].text, txt[index2].text, false)) // N^2 strings comprassions
                        if (!txt[index2].exported && clearTxt[index1].Item1 == clearTxt[index2].Item1 && clearTxt[index1].Item2 == clearTxt[index2].Item2)
                        {
                            txt_export.Add(txt[index2]);
                            txt[index2].exported = true;
                        }
                    }
                }
            }
            return txt_export;
        }

        public static string DeleteCommentsAndOther(string str)
        {
            if (str != null && str != string.Empty)
            {
                str = str.ToLower();

                var sb = new StringBuilder(str);
                sb.Replace(".", string.Empty);
                sb.Replace(" ", string.Empty);
                sb.Replace("!", string.Empty);
                sb.Replace("?", string.Empty);
                sb.Replace("-", string.Empty);
                sb.Replace(",", string.Empty);
                sb.Replace(":", string.Empty);

                str = sb.ToString().Trim();
            }
            return str;
        }
    }
}
