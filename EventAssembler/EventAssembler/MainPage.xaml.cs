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
    String textFile, binaryFile, game;
    StringWriter lastMessages;

    public MainPage()
	{
		InitializeComponent();
    }

    public async Task<FileResult> SelectFile(string title, FilePickerFileType fileType)
    {
        await Permissions.RequestAsync<Permissions.StorageRead>();
        await Permissions.RequestAsync<Permissions.StorageWrite>();

        PickOptions options = new()
        {
            PickerTitle = $"Please select {title} file",
            FileTypes = fileType,
        };
        // Workaround for: https://github.com/dotnet/maui/issues/9394
#if MACCATALYST
        options = null;
#endif
        return await FilePicker.Default.PickAsync(options);
    }

    private async void OnLanguageRawClicked(object sender, EventArgs e)
	{
        try
        {
            var result = await SelectFile(
                "language raw",
                new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                            { DevicePlatform.iOS, new[] { "public.plain-text" } },
                            { DevicePlatform.Android, new[] { "text/plain" } },
                            { DevicePlatform.WinUI, new[] { ".txt" } },
                            { DevicePlatform.Tizen, new[] { "*/*" } },
                            { DevicePlatform.macOS, new[] { "txt" } },
                    }));
            if (result != null)
            {
                if (result.FileName.EndsWith("txt", StringComparison.OrdinalIgnoreCase))
                {
                    await LoadUserFile(result);
                    Core.Program.LoadCodes(result.FullPath, ".txt", false, false);
                    InfoText.Text = $"Loaded {result.FullPath}";
                }
                else
                {
                    InfoText.Text = "Invalid language raw file type, must be *.txt";
                }
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
            var result = await SelectFile(
                "game ROM",
                new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                            { DevicePlatform.iOS, new[] { "public.data" } },
                            { DevicePlatform.Android, new[] { "application/*" } },
                            { DevicePlatform.WinUI, new[] { ".gba", ".bin" } },
                            { DevicePlatform.Tizen, new[] { "*/*" } },
                            { DevicePlatform.macOS, new[] { "gba", "bin" } },
                    }));
            if (result != null)
            {
                if (result.FileName.EndsWith("gba", StringComparison.OrdinalIgnoreCase) ||
                    result.FileName.EndsWith("bin", StringComparison.OrdinalIgnoreCase))
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
                else
                {
                    InfoText.Text = "Invalid game ROM file type, must be *.bin or *.gba";
                }
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
            var result = await SelectFile(
                "event script",
                new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                            { DevicePlatform.iOS, new[] { "public.plain-text" } },
                            { DevicePlatform.Android, new[] { "text/plain" } },
                            { DevicePlatform.WinUI, new[] { ".event", ".txt" } },
                            { DevicePlatform.Tizen, new[] { "*/*" } },
                            { DevicePlatform.macOS, new[] { "event", "txt" } },
                    }));
            if (result != null)
            {
                if (result.FileName.EndsWith("event", StringComparison.OrdinalIgnoreCase) ||
                    result.FileName.EndsWith("txt", StringComparison.OrdinalIgnoreCase))
                {
                    textFile = await LoadUserFile(result);
                    InfoText.Text = $"Loaded {textFile}";
                }
                else
                {
                    InfoText.Text = "Invalid event script file type, must be *.txt or *.event";
                }
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
            if (LibraryPicker.SelectedIndex == -1)
            {
                throw new Exception("Please select library");
            }
            await LoadStandardLibrary();
            if (CorePicker.SelectedIndex == 1)
            {
                await LoadLanguageRaws();
                StringBuilder sb = new StringBuilder();
                TextWriter errorStream = new StringWriter(sb);
                var inStream = File.OpenRead(textFile);
                IOutput output = new ROM(File.Open(binaryFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                Log log = new()
                {
                    Output = errorStream,
                    WarningsAreErrors = false,
                    NoColoredTags = true
                };
                EAInterpreter myInterpreter = new EAInterpreter(output, game, FileSystem.Current.CacheDirectory, $".LanguageRaws{LibraryPicker.SelectedIndex}.txt", inStream, textFile, log);
                bool success = myInterpreter.Interpret();
                inStream.Close();
                output.Close();
                errorStream.Close();
                InfoText.Text = success ? "success": "failure" + Environment.NewLine + sb.ToString();
            }
            else
            {
                if (!Core.Program.CodesLoaded)
                {
                    await LoadLanguageRaws();
                    Core.Program.LoadCodes(FileSystem.Current.CacheDirectory, $".LanguageRaws{LibraryPicker.SelectedIndex}.txt", true, false);
                }
                lastMessages = new StringWriter();
                var messageLog = new TextWriterMessageLog(lastMessages);
                Core.Program.Assemble(textFile, binaryFile, game, messageLog);
                messageLog.PrintAll();
                InfoText.Text = lastMessages.ToString();
            }
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share game ROM",
                File = new ShareFile(binaryFile)
            });
        }
        catch (Exception ex)
        {
            InfoText.Text = ex.ToString();
        }

        SemanticScreenReader.Announce(InfoText.Text);
    }
    private async void OnDisassembleClicked(object sender, EventArgs e)
    {
        try
        {
            if (CorePicker.SelectedIndex == 1)
            {
                throw new Exception("Selected core doesn't support to disassemble");
            }
            if (binaryFile == null || game == null)
            {
                throw new Exception("Please select game");
            }
            if (offsetEntry.Text == null)
            {
                throw new Exception("Please input offset");
            }
            if (LibraryPicker.SelectedIndex == -1)
            {
                throw new Exception("Please select library");
            }
            if (!Core.Program.CodesLoaded)
            {
                await LoadLanguageRaws();
                Core.Program.LoadCodes(FileSystem.Current.CacheDirectory, $".LanguageRaws{LibraryPicker.SelectedIndex}.txt", true, false);
            }
            lastMessages = new StringWriter();
            var messageLog = new TextWriterMessageLog(lastMessages);
            var outputFile = Path.Combine(FileSystem.CacheDirectory, Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + ".txt"); 
            Core.Program.Disassemble(binaryFile, outputFile, game, true, fullChapter.IsChecked ? DisassemblyMode.Structure : DisassemblyMode.ToEnd, Convert.ToInt32(offsetEntry.Text, 16), Priority.none, 4096, messageLog);
            messageLog.PrintAll();
            InfoText.Text = lastMessages.ToString();
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share event script",
                File = new ShareFile(Path.Combine(outputFile))
            });
        }
        catch (Exception ex)
        {
            InfoText.Text = ex.ToString();
        }

        SemanticScreenReader.Announce(InfoText.Text);
    }

    public async Task<string> LoadUserFile(FileResult file)
    {
        // Read the source file
        using Stream fileStream = await file.OpenReadAsync();
        using StreamReader reader = new StreamReader(fileStream);

        string content = await reader.ReadToEndAsync();

        // Write the file content to the app data directory
        string targetFile = Path.Combine(FileSystem.Current.CacheDirectory, Path.GetFileName(file.FileName));

        using FileStream outputStream = File.Open(targetFile, FileMode.Create, FileAccess.Write, FileShare.Read);
        using StreamWriter streamWriter = new StreamWriter(outputStream);

        await streamWriter.WriteAsync(content);

        return targetFile;
    }

    public async Task LoadResource(string sourcePath, string targetPath)
    {
        // Read the source file
        using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync(sourcePath);
        using StreamReader reader = new StreamReader(fileStream);

        string content = await reader.ReadToEndAsync();

        // Write the file content to the app data directory
        string targetFile = Path.Combine(FileSystem.Current.CacheDirectory, targetPath.Replace('/', '-').Replace('\\', '-'));

        using FileStream outputStream = File.Open(targetFile, FileMode.Create, FileAccess.Write, FileShare.Read);
        using StreamWriter streamWriter = new StreamWriter(outputStream);

        await streamWriter.WriteAsync(content);
    }

    public async Task LoadLanguageRaws()
    {
        using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync($"LanguageRaws{LibraryPicker.SelectedIndex}.txt");
        using StreamReader reader = new StreamReader(fileStream);

        while (true)
        {
            var filePath = reader.ReadLine();
            if (filePath == null)
                break;
            filePath = filePath.Trim();
            if (filePath == "")
                break;
            filePath = filePath.Replace(".txt", $".LanguageRaws{LibraryPicker.SelectedIndex}.txt");
            await LoadResource(filePath, filePath);
        }
    }

    public async Task LoadStandardLibrary()
    {
        using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync($"StandardLibrary{LibraryPicker.SelectedIndex}.txt");
        using StreamReader reader = new StreamReader(fileStream);

        while (true)
        {
            var filePath = reader.ReadLine();
            if (filePath == null)
                break;
            filePath = filePath.Trim();
            if (filePath == "")
                break;
            await LoadResource($"StandardLibrary{LibraryPicker.SelectedIndex}-" + filePath, filePath);
        }

        await LoadResource($"EAstdlib{LibraryPicker.SelectedIndex}.txt", "EAstdlib.event");
    }
}

