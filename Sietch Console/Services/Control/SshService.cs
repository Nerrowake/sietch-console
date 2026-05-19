using System.IO;
using System.Runtime.CompilerServices;
using Renci.SshNet;
using SietchConsole.Core.Interfaces;
using SietchConsole.Core.Models;

namespace Sietch_Console.Services.Control;

/// <summary>
/// Persistent SSH + SFTP session to the Hyper-V VM running the battlegroup.
/// Registered as a singleton.  Call <see cref="ConnectAsync"/> once after the VM boots;
/// all subsequent operations reuse the open session.
/// </summary>
public sealed class SshService : ISshService, IDisposable
{
    private SshClient?        _ssh;
    private SftpClient?       _sftp;
    private ConnectionInfo?   _lastInfo;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsConnected => _ssh?.IsConnected == true;

    // ── Connect / Disconnect ─────────────────────────────────────────────────

    public async Task ConnectAsync(string host, int port, string username,
                                   string privateKeyPath, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await DropConnectionsAsync();

            var key  = new PrivateKeyFile(privateKeyPath);
            var auth = new PrivateKeyAuthenticationMethod(username, key);
            _lastInfo = new ConnectionInfo(host, port, username, auth);

            _ssh = new SshClient(_lastInfo);
            await Task.Run(() => _ssh.Connect(), ct);

            _sftp = new SftpClient(_lastInfo);
            await Task.Run(() => _sftp.Connect(), ct);
        }
        finally { _gate.Release(); }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync();
        try { await DropConnectionsAsync(); }
        finally { _gate.Release(); }
    }

    private Task DropConnectionsAsync()
    {
        try { _ssh?.Disconnect();  _ssh?.Dispose();  } catch { }
        try { _sftp?.Disconnect(); _sftp?.Dispose(); } catch { }
        _ssh  = null;
        _sftp = null;
        return Task.CompletedTask;
    }

    // ── Command execution ─────────────────────────────────────────────────────

    public Task<SshCommandResult> ExecuteAsync(string command, CancellationToken ct = default)
    {
        if (_ssh is null || !_ssh.IsConnected)
            return Task.FromResult(new SshCommandResult(-1, string.Empty, "SSH not connected."));

        return Task.Run(() =>
        {
            using var cmd = _ssh.RunCommand(command);
            return new SshCommandResult(cmd.ExitStatus ?? -1, cmd.Result.TrimEnd(), cmd.Error.TrimEnd());
        }, ct);
    }

    // ── Log streaming ─────────────────────────────────────────────────────────

    public async IAsyncEnumerable<string> StreamLinesAsync(
        string command,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_ssh is null || !_ssh.IsConnected) yield break;

        using var cmd  = _ssh.CreateCommand(command);
        var result     = cmd.BeginExecute();
        using var rdr  = new StreamReader(cmd.OutputStream);

        while (!ct.IsCancellationRequested)
        {
            var line = await rdr.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is not null)
            {
                yield return line;
            }
            else
            {
                if (result.IsCompleted) break;
                await Task.Delay(150, ct).ConfigureAwait(false);
            }
        }
    }

    // ── SFTP file I/O ─────────────────────────────────────────────────────────

    public Task<string> ReadRemoteFileAsync(string remotePath, CancellationToken ct = default)
    {
        if (_sftp is null || !_sftp.IsConnected)
            throw new InvalidOperationException("SFTP session is not open.");

        return Task.Run(() =>
        {
            using var stream = _sftp.OpenRead(remotePath);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }, ct);
    }

    public Task WriteRemoteFileAsync(string remotePath, string content, CancellationToken ct = default)
    {
        if (_sftp is null || !_sftp.IsConnected)
            throw new InvalidOperationException("SFTP session is not open.");

        return Task.Run(() =>
        {
            using var stream = _sftp.Create(remotePath);
            using var writer = new StreamWriter(stream);
            writer.Write(content);
        }, ct);
    }

    // ── Connection test ───────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error)> TestConnectionAsync(
        string host, int port, string username, string privateKeyPath,
        CancellationToken ct = default)
    {
        SshClient? client = null;
        try
        {
            var key    = new PrivateKeyFile(privateKeyPath);
            var auth   = new PrivateKeyAuthenticationMethod(username, key);
            var info   = new ConnectionInfo(host, port, username, auth);
            client     = new SshClient(info);
            await Task.Run(() => client.Connect(), ct);
            using var cmd = client.RunCommand("echo ok");
            return (cmd.ExitStatus is 0 or null, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            try { client?.Disconnect(); client?.Dispose(); } catch { }
        }
    }

    // ── Disposal ──────────────────────────────────────────────────────────────

    public void Dispose()
    {
        try { _ssh?.Disconnect();  _ssh?.Dispose();  } catch { }
        try { _sftp?.Disconnect(); _sftp?.Dispose(); } catch { }
        _gate.Dispose();
    }
}
