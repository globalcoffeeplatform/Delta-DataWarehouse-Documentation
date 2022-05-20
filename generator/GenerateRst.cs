using System.Diagnostics;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Generator;

public sealed class GenerateRst
{
    private const string WorkingPath = @"D:\dev\GlobalCoffeePlatform\DeltaDataWarehouse\git\";
    private readonly string _jsonSchemaFile = Path.Combine(WorkingPath, @"schema\farmData.schema.json");
    private readonly string _outputRstFile = Path.Combine(WorkingPath, @"docs\source\explanation.rst");
    private readonly string _testdataJsonFile = Path.Combine(WorkingPath, @"example-data\testset.json");

    private TitleNumber _titleNumber;
    private readonly StringBuilder _sb = new();

    private JArray? _required;

    public GenerateRst()
    {
        _titleNumber = new TitleNumber();
    }

    public void ParseDeltaDataWarehouseSchema()
    {
        var jsonSchemaObject = JObject.Parse(File.ReadAllText(_jsonSchemaFile));
        Debug.Assert(jsonSchemaObject is not null, "No JSON schema parsed");

        WriteSchemaHeaderProperties(jsonSchemaObject);

        // Write content:
        _sb.AppendLine("\n.. contents::\n    :depth: 4");

        var properties = jsonSchemaObject.Property("properties");
        if (properties is null) throw new InvalidOperationException("No properties found");
        
        var propertyList = new List<JProperty>(); // To hold the properties for later usage
        var rootProperties = JObject.Parse(properties.Value.ToString());
        foreach (var property in rootProperties.Properties())
        {
            var objectProperties = JObject.Parse(property.Value.ToString());
            var type = objectProperties.GetValue("type");
            if (type is null) throw new InvalidOperationException("No type found");
            
            if (type.ToString() == "object")
            {
                propertyList.Add(property);
                continue; // Save for later
            }

            WriteProperties(property.Name, objectProperties);
            _titleNumber.Level2++;
        }

        // Now parse next level:
        WriteNextLevel(propertyList, 2, _required);

        // Write definitions:
        //SetTitleNumbers(2);
        //WriteHeading("Definitions", 2);
        //SetTitleNumbers(3);
        //var definitionsProperties = JObject.Parse(jsonObject.Property("definitions").Value.ToString());
        //foreach (var property in definitionsProperties.Properties())
        //{
        //    var objectProperties = JObject.Parse(property.Value.ToString());
        //    _sb.AppendLine($"\n.. _definitions_{property.Name.ToLower()}:");
        //    WriteProperties(property.Name, objectProperties);
        //    _titleNumber.Level2++;
        //}

        SplitJsonTestdata();

        SaveFile();
    }

    public void SplitJsonTest()
    {
        SplitJsonTestdata();
    }

    private void SplitJsonTestdata()
    {
        if (!File.Exists(_testdataJsonFile)) throw new FileNotFoundException(_testdataJsonFile);

        var basePath = Path.GetDirectoryName(_testdataJsonFile);
        if (basePath == null) throw new ArgumentNullException($"Can't get base path from {_testdataJsonFile}");

        Debug.WriteLine(basePath);
        dynamic jsonObject = JObject.Parse(File.ReadAllText(_testdataJsonFile));
        Debug.Assert(jsonObject is not null, "No JSON parsed");

        File.WriteAllText(Path.Combine(basePath, "metadata.json"), "\"metadata\": " + jsonObject.metadata.ToString());

        File.WriteAllText(Path.Combine(basePath, "farmer-general.json"), "\"general-farmer\": " + jsonObject["general-farmer"].ToString());
        File.WriteAllText(Path.Combine(basePath, "farmer-social.json"), "\"social-farmer\": " + jsonObject["social-farmer"].ToString());

        File.WriteAllText(Path.Combine(basePath, "farm-general.json"), "\"general-farm\": " + jsonObject["general-farm"].ToString());
        File.WriteAllText(Path.Combine(basePath, "farm-social.json"), "\"social-farm\": " + jsonObject["social-farm"].ToString());
        File.WriteAllText(Path.Combine(basePath, "farm-economic.json"), "\"economic-farm\": " + jsonObject["economic-farm"].ToString());
        File.WriteAllText(Path.Combine(basePath, "farm-environmental.json"), "\"environmental-farm\": " + jsonObject["environmental-farm"].ToString());
    }

    private void SaveFile()
    {
        using (var file = new StreamWriter(_outputRstFile, false))
        {
            file.WriteLine(_sb.ToString());
            Debug.WriteLine($"File ({_outputRstFile}) saved");
        }

        // Rebuild
        var p = new Process();
        var psi = new ProcessStartInfo
        {
            FileName = "CMD.EXE",
            Arguments = @$"/K {WorkingPath}\docs\make.bat html"
        };
        p.StartInfo = psi;
        p.Start();
        p.WaitForExit();
    }

    private void WriteNextLevel(IEnumerable<JProperty> propertyList, int headerLevel, JArray? parentRequired)
    {
        foreach (var property in propertyList)
        {
            SetTitleNumbers(headerLevel);

            var objectProperties = JObject.Parse(property.Value.ToString());
            _required = parentRequired;
            WriteProperties(property.Name, objectProperties, headerLevel);

            var objectPropertiesType = objectProperties.GetValue("type");
            if (objectPropertiesType is null) continue;
            if (objectPropertiesType.ToString() != "object") continue;

            _required = objectProperties.GetValue("required") as JArray;
            var objectPropertiesProperties = objectProperties.GetValue("properties");
            if (objectPropertiesProperties is null) throw new InvalidOperationException("No properties found");
            var subProperties = JObject.Parse(objectPropertiesProperties.ToString());
            foreach (var subProperty in subProperties)
            {
                if (subProperty.Value is null) continue;
                var subObjectProperties = JObject.Parse(subProperty.Value.ToString());
                var subObjectPropertiesType = subObjectProperties.GetValue("type");
                if (subObjectPropertiesType is not null)
                {
                    // TODO: Check array value
                    if (subObjectPropertiesType.ToString() == "object")
                    {
                        SetTitleNumbers(headerLevel + 1);
                        WriteProperties(subProperty.Key, subObjectProperties, headerLevel + 1);
                        var properties = subObjectProperties.Property("properties");
                        if (properties is null) continue;
                        WriteNextLevel(JObject.Parse(properties.Value.ToString()).Properties().ToList(),
                            headerLevel + 2,
                            subObjectProperties.GetValue("required") as JArray);
                        SetTitleNumbers(headerLevel + 2);
                        continue;
                    }
                }

                SetTitleNumbers(headerLevel + 1);
                WriteProperties(subProperty.Key, subObjectProperties, headerLevel + 1);
            }
        }
    }

    private void SetTitleNumbers(int headerLevel)
    {
        switch (headerLevel)
        {
            case 2:
                _titleNumber.Level1++;
                _titleNumber.Level2 = 0;
                break;
            case 3:
                _titleNumber.Level2++;
                _titleNumber.Level3 = 0;
                break;
            case 4:
                _titleNumber.Level3++;
                _titleNumber.Level4 = 0;
                break;
            case 5:
                _titleNumber.Level4++;
                _titleNumber.Level5 = 0;
                break;
        }
    }

    private void WriteHeading(string text, int level)
    {
        // text = $"{_titleNumber} {text} ({level})";
        text = $"{_titleNumber} {text}";
        switch (level)
        {
            case 1:
                WriteHeading1(text);
                break;
            case 2:
                WriteHeading2(text);
                break;
            case 3:
                WriteHeading3(text);
                break;
            case 4:
                WriteHeading4(text);
                break;
            case 5:
                WriteHeading5(text);
                break;
            default:
                WriteHeading5(text);
                break;
        }
    }

    private void WriteProperties(string propertyName, JObject objectProperties, int headingLevel = 3)
    {
        var title = propertyName;
        var objectPropertiesTitle = objectProperties.GetValue("title");
        if (objectPropertiesTitle is not null)
        {
            title = objectPropertiesTitle.ToString();
        }
        else
        {
            Console.WriteLine($"{propertyName} has not title!");
        }
        WriteHeading(title, headingLevel);

        // TODO: Is not yet working properly:
        //var isMandatory = IsMandatory(propertyName);
        //_sb.AppendLine("This field is " + MakeBold(isMandatory ? "Mandatory" : "Optional") + "\n");

        // if (objectProperties.ContainsKey("title")) _sb.AppendLine(objectProperties.GetValue("title") + "\n");

        WriteTechnicalDetails(propertyName, objectProperties);

        WriteDescription(objectProperties);

        WriteReference(objectProperties);

        WriteExampleData(objectProperties);
    }

    private void WriteTechnicalDetails(string propertyName, JObject objectProperties)
    {
        _sb.AppendLine(".. topic:: Details:\n");
        _sb.AppendLine($"   **Property name**: {propertyName}\n");
        WriteType(objectProperties);

        // TODO WriteDefinition(objectProperties);

        WritePattern(objectProperties);
    }

    //private void WriteDefinition(JObject objectProperties)
    //{
    //    var objectPropertiesRef = objectProperties.GetValue("$ref");
    //    if (objectPropertiesRef is null) return;

    //    var reference = objectPropertiesRef.ToString();
    //    if (!reference.StartsWith("#/definitions/")) return;

    //    var definition = reference.Replace("#/definitions/", "");
    //    _sb.AppendLine($"{MakeBold("Type")}: See :ref:`definitions_{definition.ToLower()}`");
    //}

    private void WritePattern(JObject objectProperties)
    {
        var pattern = objectProperties.GetValue("pattern");
        if (pattern is not null)
            _sb.AppendLine("   " + MakeBold("Pattern") + ": " + MakeItalic(pattern.ToString()) + "\n");

        var patternValidator = objectProperties.GetValue("$pattern-validator");
        if (patternValidator is not null)
            _sb.AppendLine(MakeLink("Pattern validator", patternValidator.ToString()));
    }

    private void WriteDescription(JObject objectProperties)
    {
        var description = objectProperties.GetValue("description");
        if (description is not null)
        {
            AppendMultiLines(AppendDotIfNeeded(description.ToString()));
        }
        else
        {
            Console.WriteLine($"{objectProperties} has no description");
        }

        var extendedDescription = objectProperties.GetValue("$extended-description");
        if (extendedDescription is not null)
            AppendMultiLines(AppendDotIfNeeded(extendedDescription.ToString()));
    }

    private static string AppendDotIfNeeded(string value)
    {
        // Check for dot:
        if (!value.Trim().EndsWith('.') 
            && !value.Trim().EndsWith('?')
            && !value.Trim().EndsWith(':'))
        {
            value = $"{value}.";
        }

        return value;
    }
    
    private void WriteReference(JObject objectProperties)
    {
        var objectPropertiesRef = objectProperties.GetValue("$ref");
        if (objectPropertiesRef is null) return;

        var reference = objectPropertiesRef.ToString();
        if (!reference.StartsWith("./")) return;

        var file = reference.Replace("./", "/");
        //_sb.AppendLine($"\n.. literalinclude:: ../../schema{file}");
        //_sb.AppendLine("   :language: json");
        //_sb.AppendLine("   :linenos:");
        //_sb.AppendLine($"   :caption: {reference.Replace("./", "")}");
        _sb.AppendLine($"\n.. raw:: html\n");
        _sb.AppendLine($"    <script src=\"_static/docson/widget.js\" data-schema=\"../schema{file}\"></script>\n");
    }

    private void WriteExampleData(JObject objectProperties)
    {
        var exampleDataKey = objectProperties.GetValue("$example-data");
        if (exampleDataKey is null) return;

        var exampleData = exampleDataKey.ToString();
        if (exampleData.StartsWith("./"))
        {
            var path = exampleData.Replace("./", "_static/");
            var pos = path.LastIndexOf("/", StringComparison.Ordinal) + 1;
            var title = path.Substring(pos, path.Length - pos).Replace(".json", "");
            var id = $"example-data-{title}";
            title = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(title.ToLower().Replace("-", " ")) + " Example Data";

            _sb.AppendLine("\n.. raw:: html\n");
            _sb.AppendLine($"    <button class=\"btn btn-example-data\" onclick=\"$('#{id}').toggle(300)\">{title}</button>");
            _sb.AppendLine($"    <div id=\"{id}\" style=\"display: none;\">");

            _sb.AppendLine($"\n.. literalinclude:: {path}");
            _sb.AppendLine("   :linenos:");

            _sb.AppendLine("\n.. raw:: html\n");
            _sb.AppendLine($"    </div>");
        }
        else
        {
            _sb.AppendLine("\n.. code-block:: json-object");
            _sb.AppendLine("   :linenos:");
            _sb.AppendLine("   :caption: Sample data");
            _sb.AppendLine("");
            _sb.AppendLine($"    {exampleData}\n");
        }
    }

    private void WriteType(JObject objectProperties)
    {
        var type = objectProperties.GetValue("type");
        if (type is not null)
            _sb.AppendLine("   " + MakeBold("Type") + ": " + MakeItalic(type.ToString()) + "\n");

        if (objectProperties.ContainsKey("enum"))
        {
            if (objectProperties.GetValue("enum") is JArray enums)
                _sb.AppendLine("   " + MakeBold("Allowed values") + ": '" + string.Join("', '", enums) + "'\n");
        }

        var minimum = objectProperties.GetValue("minimum");
        if (minimum is not null)
            _sb.AppendLine("   " + MakeBold("Minimum") + ": " + MakeItalic(minimum.ToString()) + "\n");

        var exclusiveMinimum = objectProperties.GetValue("exclusiveMinimum");
        if (exclusiveMinimum is not null)
            _sb.AppendLine("   " + MakeBold("Exclusive minimum") + ": " + MakeItalic(exclusiveMinimum.ToString()) + "\n");

        var maximum = objectProperties.GetValue("maximum");
        if (maximum is not null)
            _sb.AppendLine("   " + MakeBold("Maximum") + ": " + MakeItalic(maximum.ToString()) + "\n");

        var exclusiveMaximum = objectProperties.GetValue("exclusiveMaximum");
        if (exclusiveMaximum is not null)
            _sb.AppendLine("   " + MakeBold("Exclusive maximum") + ": " + MakeItalic(exclusiveMaximum.ToString()) + "\n");

        if (objectProperties.ContainsKey("examples"))
        {
            if (objectProperties.GetValue("examples") is JArray examples)
                _sb.AppendLine("   " + MakeBold("Examples") + ": '" + string.Join("', '", examples) + "'\n");
        }

        // Array properties:
        var uniqueItems = objectProperties.GetValue("uniqueItems");
        if (uniqueItems is not null)
            _sb.AppendLine("   " + MakeBold("Unique items") + ": " + MakeItalic(uniqueItems.ToString()) + "\n");

        var minItems = objectProperties.GetValue("minItems");
        if (minItems is not null)
            _sb.AppendLine("   " + MakeBold("Minimum items") + ": " + MakeItalic(minItems.ToString()) + "\n");

        var objectPropertiesItems = objectProperties.GetValue("items");
        if (objectPropertiesItems is not null)
        {
            foreach (var jToken in objectPropertiesItems)
            {
                var tmp = (JProperty)jToken;
                _sb.AppendLine("   " + MakeBold("Array items") + ": " +
                               MakeItalic(tmp.Value.ToString().Replace("./", "")) + "\n");

            }
            if (objectProperties.GetValue("items") is JArray items)
                _sb.AppendLine("   " + MakeBold("items") + ": '" + string.Join("', '", items) + "'\n");
        }

        var objectPropertiesRef = objectProperties.GetValue("$ref");
        if (objectPropertiesRef is not null)
            _sb.AppendLine("   " + MakeBold("Reference") + ": " +
                           MakeItalic(objectPropertiesRef.ToString().Replace("./", "")) + "\n");
    }

    //private bool IsMandatory(string propertyName)
    //{
    //    if (_required == null) return false;

    //    foreach (var name in _required)
    //    {
    //        if (name.ToString() == propertyName) return true;
    //    }

    //    return false;
    //}

    private void AppendMultiLines(string text)
    {
        var lines = text.Split(new[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);
        _sb.AppendLine();
        foreach (var line in lines)
        {
            _sb.AppendLine($"{line}\n");
        }
    }

    private void WriteSchemaHeaderProperties(dynamic jsonObject)
    {
        WriteHeading((string)jsonObject.title, 1);
        string description = jsonObject.description;
        _required = jsonObject.required;
        _sb.AppendLine(description);
        _sb.AppendLine($"\n{MakeBold("Version")}: {jsonObject.version}\n");
    }

    private void WriteHeading1(string text)
    {
        _sb.AppendLine("");
        _sb.AppendLine(new string('=', text.Length));
        _sb.AppendLine(text);
        _sb.AppendLine(new string('=', text.Length));
    }

    private void WriteHeading2(string text)
    {
        _sb.AppendLine("");
        _sb.AppendLine(new string('*', text.Length));
        _sb.AppendLine(text);
        _sb.AppendLine(new string('*', text.Length));
    }

    private void WriteHeading3(string text)
    {
        _sb.AppendLine("");
        _sb.AppendLine(text);
        _sb.AppendLine(new string('^', text.Length));
    }

    private void WriteHeading4(string text)
    {
        _sb.AppendLine("");
        _sb.AppendLine(text);
        _sb.AppendLine(new string('-', text.Length));
    }

    private void WriteHeading5(string text)
    {
        _sb.AppendLine("");
        _sb.AppendLine(text);
        _sb.AppendLine(new string('*', text.Length));
    }

    private static string MakeBold(string text)
    {
        return $"**{text}**";
    }

    private static string MakeItalic(string text)
    {
        return $"*{text}*";
    }

    private static string MakeLink(string label, string url)
    {
        return $".. raw:: html \n\n   <a href=\"{url}\" target=\"_blank\">{label}</a>\n";
    }

    private struct TitleNumber
    {
        public int Level1 { get; set; }
        public int Level2 { get; set; }
        public int Level3 { get; set; }
        public int Level4 { get; set; }
        public int Level5 { get; set; }

        public override string ToString()
        {
            var text = string.Empty;

            if (Level1 <= 0) return text;

            text = Level1.ToString();
            if (Level2 <= 0) return text;

            text = $"{Level1}.{Level2}";
            if (Level3 <= 0) return text;

            text = $"{Level1}.{Level2}.{Level3}";
            if (Level4 <= 0) return text;

            text = $"{Level1}.{Level2}.{Level3}.{Level4}";
            if (Level5 > 0) text = $"{Level1}.{Level2}.{Level3}.{Level4}.{Level5}";
            return text;
        }
    }

}

