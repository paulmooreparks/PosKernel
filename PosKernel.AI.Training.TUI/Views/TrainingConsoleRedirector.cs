/*
Copyright 2025 Paul Moore Parks and contributors

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System.Text;

namespace PosKernel.AI.Training.TUI.Views;

/// <summary>
/// Redirects Console.Out and Console.Error to the Training TUI Activity Log.
/// Prevents console output from corrupting the TUI display while capturing all output.
/// ARCHITECTURAL PRINCIPLE: Same console capture pattern as PosKernel.AI demo
/// </summary>
internal class TrainingConsoleRedirector : TextWriter
{
    private readonly Action<string> _addLogAction;
    private readonly TextWriter _originalWriter;
    private readonly string _streamType;

    public TrainingConsoleRedirector(Action<string> addLogAction, TextWriter originalWriter, string streamType)
    {
        _addLogAction = addLogAction ?? throw new ArgumentNullException(nameof(addLogAction));
        _originalWriter = originalWriter ?? throw new ArgumentNullException(nameof(originalWriter));
        _streamType = streamType ?? throw new ArgumentNullException(nameof(streamType));
    }

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        // Buffer single characters - most console output comes as complete strings
    }

    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _addLogAction($"[CONSOLE.{_streamType}] {value}");
        }
    }

    public override void WriteLine(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _addLogAction($"[CONSOLE.{_streamType}] {value}");
        }
    }

    public override void WriteLine()
    {
        // Ignore empty lines to reduce log spam
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Restore original console streams
            if (_streamType == "OUT")
            {
                System.Console.SetOut(_originalWriter);
            }
            else if (_streamType == "ERROR")
            {
                System.Console.SetError(_originalWriter);
            }
        }
        base.Dispose(disposing);
    }
}
