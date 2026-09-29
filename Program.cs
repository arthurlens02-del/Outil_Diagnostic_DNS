using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace OutilDiagnosticDNS;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class CommandResult
{
    public int ExitCode { get; init; }
    public string Output { get; init; } = "";
    public bool Success => ExitCode == 0;
}

public sealed class MainForm : Form
{
    readonly Label stepLabel = new();
    readonly Label titleLabel = new();
    readonly Label explanationLabel = new();
    readonly RichTextBox powershellBox = new();
    readonly RichTextBox resultBox = new();
    readonly Button nextButton = new();
    readonly Button backButton = new();
    readonly Button runButton = new();
    readonly Button repairButton = new();
    readonly ProgressBar progress = new();

    int step = 1;
    string gateway = "";
    string dnsServer = "";
    bool dnsTestOk;
    bool internetIpOk;

    public MainForm()
    {
        Text = "Outil de diagnostic DNS — Portfolio IT";
        Width = 1120;
        Height = 760;
        MinimumSize = new Size(980, 650);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);

        BuildUi();
        ShowStep(1);
    }

    void BuildUi()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 90, Padding = new Padding(24, 14, 24, 8) };
        titleLabel.Text = "Outil de diagnostic DNS";
        titleLabel.Font = new Font("Segoe UI", 23, FontStyle.Bold);
        titleLabel.AutoSize = true;

        stepLabel.Location = new Point(2, 50);
        stepLabel.AutoSize = true;
        stepLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);

        header.Controls.Add(titleLabel);
        header.Controls.Add(stepLabel);

        progress.Dock = DockStyle.Top;
        progress.Height = 7;
        progress.Maximum = 10;
        progress.Minimum = 0;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24, 18, 24, 12) };

        explanationLabel.Location = new Point(0, 55);
        explanationLabel.Size = new Size(1020, 65);
        explanationLabel.Font = new Font("Segoe UI", 11);

        powershellBox.Location = new Point(0, 140);
        powershellBox.Size = new Size(1020, 190);
        powershellBox.ReadOnly = true;
        powershellBox.BackColor = Color.FromArgb(15, 15, 15);
        powershellBox.ForeColor = Color.FromArgb(230, 230, 230);
        powershellBox.Font = new Font("Cascadia Mono", 10);
        powershellBox.BorderStyle = BorderStyle.FixedSingle;

        resultBox.Location = new Point(0, 350);
        resultBox.Size = new Size(1020, 130);
        resultBox.ReadOnly = true;
        resultBox.BackColor = Color.FromArgb(245, 246, 248);
        resultBox.Font = new Font("Segoe UI", 10);
        resultBox.BorderStyle = BorderStyle.FixedSingle;

        runButton.Text = "▶ Exécuter la commande";
        runButton.Location = new Point(0, 500);
        runButton.Size = new Size(220, 45);
        runButton.Click += async (_, _) => await ExecuteCurrentStep();

        repairButton.Text = "🔧 Réparer";
        repairButton.Location = new Point(235, 500);
        repairButton.Size = new Size(150, 45);
        repairButton.Visible = false;
        repairButton.Click += async (_, _) => await ExecuteCurrentStep();

        backButton.Text = "← Précédent";
        backButton.Location = new Point(0, 555);
        backButton.Size = new Size(150, 42);
        backButton.Click += (_, _) => { if (step > 1) ShowStep(--step); };

        nextButton.Text = "Étape suivante →";
        nextButton.Location = new Point(165, 555);
        nextButton.Size = new Size(180, 42);
        nextButton.Click += (_, _) => { if (step < 10) ShowStep(++step); };

        content.Controls.Add(explanationLabel);
        content.Controls.Add(powershellBox);
        content.Controls.Add(resultBox);
        content.Controls.Add(runButton);
        content.Controls.Add(repairButton);
        content.Controls.Add(backButton);
        content.Controls.Add(nextButton);

        Controls.Add(content);
        Controls.Add(progress);
        Controls.Add(header);
    }

    void ShowStep(int number)
    {
        step = number;
        progress.Value = number;
        stepLabel.Text = $"Étape {number} / 10";

        powershellBox.Clear();
        resultBox.Clear();
        repairButton.Visible = false;

        switch (number)
        {
            case 1:
                SetStep(
                    "1. Vérifier la configuration réseau",
                    "On commence par relever l'adresse IPv4, la passerelle et surtout les serveurs DNS utilisés par le PC.",
                    "ipconfig /all");
                break;

            case 2:
                SetStep(
                    "2. Vérifier le réseau local",
                    "On vérifie que le PC peut communiquer avec sa passerelle.",
                    gateway.Length > 0 ? $"ping {gateway}" : "ping <passerelle>");
                break;

            case 3:
                SetStep(
                    "3. Tester Internet sans utiliser le DNS",
                    "On teste une adresse IP directement. Cela permet de distinguer un problème de connectivité IP d'un problème de résolution de noms.",
                    "ping 8.8.8.8");
                break;

            case 4:
                SetStep(
                    "4. Tester la résolution DNS",
                    "On demande à DNS de résoudre google.com. Si le test IP précédent fonctionne mais celui-ci échoue, le DNS devient une piste probable.",
                    "nslookup google.com");
                break;

            case 5:
                SetStep(
                    "5. Tester le DNS de l'entreprise",
                    "On peut tester directement le serveur DNS de l'entreprise. Entre son adresse IP ci-dessous avant d'exécuter la commande.",
                    "nslookup google.com <DNS_ENTREPRISE>");
                break;

            case 6:
                SetStep(
                    "6. Vérifier le DNS configuré",
                    "Si le PC utilise un mauvais DNS, il faut remettre le DNS prévu par l'entreprise. Cette application n'impose pas une adresse DNS automatiquement.",
                    "ncpa.cpl → Propriétés → TCP/IPv4 → DNS");
                break;

            case 7:
                SetStep(
                    "7. Renouveler DHCP si le DNS est automatique",
                    "Si la configuration doit venir automatiquement du DHCP, on peut libérer puis renouveler le bail.",
                    "ipconfig /release\nipconfig /renew");
                repairButton.Visible = true;
                repairButton.Text = "🔧 Renouveler DHCP";
                break;

            case 8:
                SetStep(
                    "8. Vider le cache DNS",
                    "Cela supprime les anciennes résolutions DNS mémorisées localement.",
                    "ipconfig /flushdns");
                repairButton.Visible = true;
                repairButton.Text = "🔧 Vider le cache DNS";
                break;

            case 9:
                SetStep(
                    "9. Retester",
                    "Après la correction, on vérifie à nouveau la résolution du nom puis la connectivité par nom.",
                    "nslookup google.com\nping google.com");
                break;

            case 10:
                SetStep(
                    "10. Vérification avancée du port DNS",
                    "Si le bon DNS est déjà configuré mais ne répond pas, on peut vérifier l'accès au port 53 du serveur DNS.",
                    dnsServer.Length > 0
                        ? $"Test-NetConnection {dnsServer} -Port 53"
                        : "Test-NetConnection <DNS_ENTREPRISE> -Port 53");
                break;
        }
    }

    void SetStep(string title, string explanation, string command)
    {
        titleLabel.Text = "Outil de diagnostic DNS";
        stepLabel.Text = $"Étape {step} / 10";
        explanationLabel.Text = $"{title}\r\n\r\n{explanation}";
        powershellBox.Text = $"PS C:\\IT-Support> {command}";
    }

    async Task ExecuteCurrentStep()
    {
        SetBusy(true);

        try
        {
            switch (step)
            {
                case 1:
                    {
                        var r = await Run("ipconfig /all");
                        gateway = ExtractGateway(r.Output);
                        dnsServer = ExtractFirstDns(r.Output);
                        ShowResult(r, gateway.Length > 0
                            ? $"✓ Configuration récupérée.\r\nPasserelle détectée : {gateway}\r\nDNS détecté : {(dnsServer.Length > 0 ? dnsServer : "non détecté")}"
                            : "⚠ Configuration récupérée, mais la passerelle n'a pas été détectée automatiquement.");
                    }
                    break;

                case 2:
                    {
                        if (string.IsNullOrWhiteSpace(gateway))
                        {
                            MessageBox.Show("Exécute d'abord l'étape 1 pour récupérer la passerelle.", "Étape précédente requise",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        var r = await Run($"ping -n 2 {gateway}");
                        ShowResult(r, r.Success
                            ? "✓ La passerelle répond. Le réseau local est joignable."
                            : "✗ La passerelle ne répond pas. Le problème est probablement avant le DNS.");
                    }
                    break;

                case 3:
                    {
                        var r = await Run("ping -n 2 8.8.8.8");
                        internetIpOk = r.Success;
                        ShowResult(r, r.Success
                            ? "✓ Internet fonctionne au niveau IP. Le DNS reste une piste possible."
                            : "✗ Internet n'est pas joignable par IP. Le problème n'est pas uniquement le DNS.");
                    }
                    break;

                case 4:
                    {
                        var r = await Run("nslookup google.com");
                        dnsTestOk = r.Success && !r.Output.Contains("server failed", StringComparison.OrdinalIgnoreCase);
                        ShowResult(r, dnsTestOk
                            ? "✓ Le nom google.com a été résolu."
                            : "✗ La résolution DNS semble échouer.");
                    }
                    break;

                case 5:
                    {
                        if (string.IsNullOrWhiteSpace(dnsServer))
                        {
                            MessageBox.Show("Le DNS n'a pas été détecté. Exécute l'étape 1 ou saisis-le dans le code de la commande.", "DNS manquant",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        var r = await Run($"nslookup google.com {dnsServer}");
                        ShowResult(r, r.Success
                            ? $"✓ Le serveur DNS {dnsServer} a répondu."
                            : $"✗ Le serveur DNS {dnsServer} n'a pas répondu correctement.");
                    }
                    break;

                case 6:
                    MessageBox.Show(
                        "Ouvre ncpa.cpl → carte réseau → Propriétés → IPv4 → Propriétés.\r\n\r\n" +
                        "Remets le DNS prévu par l'entreprise.\r\n\r\n" +
                        "Ne remplace pas automatiquement le DNS d'entreprise par 1.1.1.1 ou 8.8.8.8.",
                        "Vérification du DNS",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;

                case 7:
                    await RepairDhcp();
                    break;

                case 8:
                    await RepairDns();
                    break;

                case 9:
                    {
                        var r1 = await Run("nslookup google.com");
                        var r2 = await Run("ping -n 2 google.com");
                        ShowResult(r2, r1.Success && r2.Success
                            ? "✓ Le nom est résolu et le ping par nom fonctionne. La résolution DNS semble rétablie."
                            : "⚠ Le problème persiste ou nécessite une analyse supplémentaire.");
                    }
                    break;

                case 10:
                    {
                        if (string.IsNullOrWhiteSpace(dnsServer))
                        {
                            MessageBox.Show("Exécute l'étape 1 afin de détecter le DNS.", "DNS manquant",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        var r = await RunPowerShell($"Test-NetConnection {dnsServer} -Port 53");
                        ShowResult(r, r.Output.Contains("TcpTestSucceeded : True", StringComparison.OrdinalIgnoreCase)
                            ? $"✓ Le port 53 de {dnsServer} est accessible."
                            : $"✗ Le port 53 de {dnsServer} n'est pas accessible ou le test a échoué.");
                    }
                    break;
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    async Task RepairDhcp()
    {
        SetBusy(true);
        try
        {
            var r1 = await Run("ipconfig /release");
            var r2 = await Run("ipconfig /renew");
            ShowResult(r2, r1.Success && r2.Success
                ? "✓ DHCP renouvelé. La configuration réseau a été redemandée au serveur DHCP."
                : "⚠ Une des commandes DHCP a rencontré un problème.");
        }
        finally { SetBusy(false); }
    }

    async Task RepairDns()
    {
        SetBusy(true);
        try
        {
            var r = await Run("ipconfig /flushdns");
            ShowResult(r, r.Success
                ? "✓ Cache DNS vidé. Passe à l'étape 9 pour retester."
                : "✗ La commande de vidage du cache DNS a échoué.");
        }
        finally { SetBusy(false); }
    }

    async Task<CommandResult> Run(string command)
    {
        powershellBox.Clear();
        powershellBox.AppendText($"PS C:\\IT-Support> {command}\r\n\r\n");

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c " + command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;
            var combined = string.IsNullOrWhiteSpace(error) ? output : output + Environment.NewLine + error;

            powershellBox.AppendText(combined.TrimEnd());
            return new CommandResult { ExitCode = process.ExitCode, Output = combined };
        }
        catch (Exception ex)
        {
            powershellBox.AppendText("[ERREUR] " + ex.Message);
            return new CommandResult { ExitCode = -1, Output = ex.Message };
        }
    }

    async Task<CommandResult> RunPowerShell(string command)
    {
        powershellBox.Clear();
        powershellBox.AppendText($"PS C:\\IT-Support> {command}\r\n\r\n");

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await outputTask;
            var error = await errorTask;
            var combined = string.IsNullOrWhiteSpace(error) ? output : output + Environment.NewLine + error;

            powershellBox.AppendText(combined.TrimEnd());
            return new CommandResult { ExitCode = process.ExitCode, Output = combined };
        }
        catch (Exception ex)
        {
            powershellBox.AppendText("[ERREUR] " + ex.Message);
            return new CommandResult { ExitCode = -1, Output = ex.Message };
        }
    }

    void ShowResult(CommandResult r, string conclusion)
    {
        resultBox.Clear();
        resultBox.AppendText(r.Success ? "RÉSULTAT\r\n\r\n" : "RÉSULTAT — À VÉRIFIER\r\n\r\n");
        resultBox.AppendText(conclusion);
        resultBox.AppendText("\r\n\r\nCommande terminée avec le code : " + r.ExitCode);
    }

    string ExtractGateway(string text)
    {
        var matches = Regex.Matches(text, @"Default Gateway[ .:]*([0-9]{1,3}(?:\.[0-9]{1,3}){3})", RegexOptions.IgnoreCase);
        return matches.Count > 0 ? matches[0].Groups[1].Value : "";
    }

    string ExtractFirstDns(string text)
    {
        var match = Regex.Match(text, @"DNS Servers[ .:]*([0-9]{1,3}(?:\.[0-9]{1,3}){3})", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    void SetBusy(bool busy)
    {
        runButton.Enabled = !busy;
        repairButton.Enabled = !busy;
        nextButton.Enabled = !busy;
        backButton.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }
}
