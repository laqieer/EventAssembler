using System.Text;
using Nintenlord.Event_Assembler.Core;
using Nintenlord.Event_Assembler.Core.Code.Language;
using Core = Nintenlord.Event_Assembler.Core;
using Nintenlord.Event_Assembler.Core.IO.Logs;
using ColorzCore;
using ColorzCore.IO;

namespace EventAssembler;

public partial class MainPage : ContentPage
{
    String rawsFolder, textFile, binaryFile, game;
    StringWriter lastMessages;
    FileResult scriptFile;

    public MainPage()
	{
		InitializeComponent();
    }

    public async Task<FileResult> SelectFile(IEnumerable<string> exts)
    {
        var customFileType = new FilePickerFileType(
            new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                    { DevicePlatform.iOS, exts },
                    { DevicePlatform.Android, exts },
                    { DevicePlatform.WinUI, exts },
                    { DevicePlatform.Tizen, exts },
                    { DevicePlatform.macOS, exts },
            });

        PickOptions options = new()
        {
            PickerTitle = $"Please select {exts} file",
            FileTypes = customFileType,
        };

        return await FilePicker.Default.PickAsync(options);
    }

    private async void OnLibraryClicked(object sender, EventArgs e)
	{
        try
        {
            var result = await SelectFile(new[] { ".raws" });
            if (result != null)
            {
                rawsFolder = Path.GetDirectoryName(result.FullPath);
                Program.LoadCodes(rawsFolder, ".txt", true, false);
                InfoText.Text = $"Loaded {result.FullPath}";
            }
        }
        catch (Exception ex)
        {
            InfoText.Text = ex.ToString();
        }

        SemanticScreenReader.Announce(InfoText.Text);
    }

    private async void OnBinaryClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await SelectFile(new[] { ".gba", "bin" });
            if (result != null)
            {
                binaryFile = result.FullPath;
                var buffer = new byte[4];
                using (var stream = await result.OpenReadAsync())
                {
                    stream.Seek(0xAC, SeekOrigin.Begin);
                    await stream.ReadAsync(buffer, 0, buffer.Length);
                    stream.Close();
                }
                var code = Encoding.Default.GetString(buffer);
                var codes = new Dictionary<string, string> {
                    { "AFEJ", "FE6" },
                    { "AE7E", "FE7" },
                    { "BE8E", "FE8" },
                    { "AE7J", "FE7J" },
                    { "BE8J", "FE8J" },
                };
                if (!codes.ContainsKey(code))
                    throw new Exception("Unsupported game: " + code);
                game = codes[code];
                InfoText.Text = $"Loaded {game}: {binaryFile}";
            }
        }
        catch (Exception ex)
        {
            InfoText.Text = ex.ToString();
        }

        SemanticScreenReader.Announce(InfoText.Text);
    }

    private async void OnTextClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await SelectFile(new[] { ".event", ".txt" });
            if (result != null)
            {
                textFile = result.FullPath;
                scriptFile = result;
                InfoText.Text = $"Loaded {textFile}";
            }
        }
        catch (Exception ex)
        {
            InfoText.Text = ex.ToString();
        }

        SemanticScreenReader.Announce(InfoText.Text);
    }

    private async void OnAssembleClicked(object sender, EventArgs e)
    {
        try
        {
            if (rawsFolder == null || !Core.Program.CodesLoaded)
            {
                throw new Exception("Please select library");
            }
            if (binaryFile == null || game == null)
            {
                throw new Exception("Please select game");
            }
            if (textFile == null)
            {
                throw new Exception("Please select script");
            }
            if (!File.Exists(textFile))
            {
                throw new Exception("Script file doesn't exist: " + textFile);
            }
            if (useColorzCore.IsChecked)
            {
                StringBuilder sb = new StringBuilder();
                TextWriter errorStream = new StringWriter(sb);
                var inStream = await scriptFile.OpenReadAsync();
                IOutput output = new ROM(File.Open(binaryFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                Log log = new()
                {
                    Output = errorStream,
                    WarningsAreErrors = false,
                    NoColoredTags = true
                };
                EAInterpreter myInterpreter = new EAInterpreter(output, game, rawsFolder, ".txt", inStream, textFile, log);
                bool success = myInterpreter.Interpret();
                inStream.Close();
                output.Close();
                errorStream.Close();
                InfoText.Text = success ? "success": "failure" + Environment.NewLine + sb.ToString();
            }
            else
            {
                lastMessages = new StringWriter();
                var messageLog = new TextWriterMessageLog(lastMessages);
                Program.Assemble(textFile, binaryFile, game, messageLog);
                messageLog.PrintAll();
                InfoText.Text = lastMessages.ToString();
            } 
        }
        catch (Exception ex)
        {
            InfoText.Text = ex.ToString();
        }

        SemanticScreenReader.Announce(InfoText.Text);
    }
    private void OnDisassembleClicked(object sender, EventArgs e)
    {
        try
        {
            if (!Core.Program.CodesLoaded)
            {
                throw new Exception("Please select library");
            }
            if (binaryFile == null || game == null)
            {
                throw new Exception("Please select game");
            }
            if (textFile == null)
            {
                throw new Exception("Please select script");
            }
            if (offsetEntry.Text == null)
            {
                throw new Exception("Please input offset");
            }
            lastMessages = new StringWriter();
            var messageLog = new TextWriterMessageLog(lastMessages);
            Program.Disassemble(binaryFile, textFile, game, true, fullChapter.IsChecked ? DisassemblyMode.Structure : DisassemblyMode.ToEnd, Convert.ToInt32(offsetEntry.Text, 16), Priority.none, 4096, messageLog);
            messageLog.PrintAll();
            InfoText.Text = lastMessages.ToString();
        }
        catch (Exception ex)
        {
            InfoText.Text = ex.ToString();
        }

        SemanticScreenReader.Announce(InfoText.Text);
    }
}

