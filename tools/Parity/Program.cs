using System.Text.Json;
using JiebaNet.Parity;

if (args.Length != 2) throw new ArgumentException("Usage: Parity INPUT-FIXTURE OUTPUT-JSON");
using var fixture = JsonDocument.Parse(File.ReadAllText(args[0]));
var runner = new ParityRunner();
var actual = fixture.RootElement.GetProperty("Cases").EnumerateArray()
    .Select(item => runner.Evaluate(item.GetProperty("Text").GetString()!)).ToArray();
File.WriteAllText(args[1], JsonSerializer.Serialize(actual));
Console.WriteLine($"Exported {actual.Length} cases to {args[1]}");
