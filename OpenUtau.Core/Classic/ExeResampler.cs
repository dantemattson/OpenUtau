﻿using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using NAudio.Wave;
using OpenUtau.Core;
using OpenUtau.Core.Format;
using OpenUtau.Core.Util;
using Serilog;

namespace OpenUtau.Classic {
    internal class ExeResampler : IResampler {
        public string Name { get; private set; }
        public string FilePath { get; private set; }
        public bool isLegalPlugin => _isLegalPlugin;

        readonly bool _isLegalPlugin = false;

        public ExeResampler(string filePath, string basePath) {
            if (File.Exists(filePath)) {
                FilePath = filePath;
                Name = Path.GetRelativePath(basePath, filePath);
                _isLegalPlugin = true;
            }
        }

        public float[] DoResampler(ResamplerItem args, ILogger logger) {
            string tmpFile = DoResamplerReturnsFile(args, logger);
            if (string.IsNullOrEmpty(tmpFile) || File.Exists(tmpFile)) {
                using (var waveStream = Wave.OpenFile(tmpFile)) {
                    return Wave.GetSamples(waveStream.ToSampleProvider().ToMono(1, 0));
                }
            }
            return new float[0];
        }

        public string DoResamplerReturnsFile(ResamplerItem args, ILogger logger) {
            if (!_isLegalPlugin) {
                return null;
            }
            var threadId = Thread.CurrentThread.ManagedThreadId;
            string tmpFile = args.outputFile;
            string ArgParam = FormattableString.Invariant(
                $"\"{args.inputTemp}\" \"{tmpFile}\" {MusicMath.GetToneName(args.tone)} {args.velocity} \"{BuildFlagsStr(args.flags)}\" {args.offset} {args.requiredLength} {args.consonant} {args.cutoff} {args.volume} {args.modulation} !{args.tempo} {Base64.Base64EncodeInt12(args.pitches)}");
            logger.Information($" > [thread-{threadId}] {FilePath} {ArgParam}");
            ProcessRunner.Run(FilePath, ArgParam, logger);
            return tmpFile;
        }

        string BuildFlagsStr(Tuple<string, int?>[] flags) {
            var builder = new StringBuilder();
            foreach (var flag in flags) {
                builder.Append(flag.Item1);
                if (flag.Item2.HasValue) {
                    builder.Append(flag.Item2.Value);
                }
            }
            return builder.ToString();
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int chmod(string pathname, int mode);

        public void CheckPermissions() {
            if (OS.IsWindows() || !File.Exists(FilePath)) {
                return;
            }
            int mode = (7 << 6) | (5 << 3) | 5;
            chmod(FilePath, mode);
        }

        public override string ToString() => Name;
    }
}
