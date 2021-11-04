﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OlympUI.MegaCanvas {
    public sealed class CanvasPool : IDisposable {

        public readonly CanvasManager Manager;
        public readonly GraphicsDevice Graphics;

        public bool MSAA;

        public readonly Entry[] Entries = new Entry[64];
        public int EntriesAlive = 0;
        public readonly HashSet<RenderTarget2D> Used = new();
        public long UsedMemory = 0;
        public long TotalMemory = 0;
        public int Padding = 128;
        private int SquishFrame = 0;
        public int SquishFrames = 60 * 15;
        public int MaxAgeFrames = 60 * 5;
        public int CullTarget = 32;
        public bool CullTriggered = false;

        public CanvasPool(CanvasManager manager, bool msaa) {
            Manager = manager;
            Graphics = manager.Graphics;
            MSAA = msaa;
        }

        public void Dispose() {
            for (int i = 0; i < Entries.Length; i++) {
                Entries[i].RT?.Dispose();
                Entries[i] = default;
            }
            EntriesAlive = 0;
            UsedMemory = 0;
            TotalMemory = 0;
            SquishFrame = 0;
        }

        public void Update() {
            lock (Entries) {
                if (SquishFrame++ >= SquishFrames || CullTriggered) {
                    CullTriggered = false;
                    for (int i = Entries.Length - 1; i >= 0; --i) {
                        ref Entry entry = ref Entries[i];
                        if (!entry.IsNull && (entry.IsDisposed || entry.Age++ >= MaxAgeFrames)) {
                            Dispose(ref entry);
                        }
                    }
                    for (int i = Entries.Length - 1; i >= CullTarget; --i) {
                        ref Entry entry = ref Entries[i];
                        if (!entry.IsNull) {
                            Dispose(ref entry);
                        }
                    }

                } else {
                    for (int i = Entries.Length - 1; i >= 0; --i) {
                        ref Entry entry = ref Entries[i];
                        if (!entry.IsDisposed && entry.Age++ >= MaxAgeFrames) {
                            Dispose(ref entry);
                        }
                    }
                }
            }
        }

        private void Dispose(ref Entry entry) {
            entry.RT?.Dispose();
            TotalMemory -= entry.RT?.GetMemoryUsage() ?? 0;
            entry = default;
            EntriesAlive--;
        }

        public RenderTarget2DRegion? Get(int width, int height) {
            if (width < Manager.MinSize || height < Manager.MinSize ||
                width > Manager.MaxSize || height > Manager.MaxSize)
                return null;

            RenderTarget2D? rt = null;
            bool fresh;

            lock (Entries) {
                if (Entries.TryGetSmallest(width, height, out Entry entry, out int index)) {
                    Entries[index] = default;
                    EntriesAlive--;
                    rt = entry.RT;
                }
            }

            if (rt == null) {
                int widthReal = Math.Min(Manager.MaxSize, (int) MathF.Ceiling(width / Padding + 1) * Padding);
                int heightReal = Math.Min(Manager.MaxSize, (int) MathF.Ceiling(height / Padding + 1) * Padding);
                rt = new(Graphics, widthReal, heightReal, false, SurfaceFormat.Color, DepthFormat.None, MSAA ? Manager.MultiSampleCount : 0, RenderTargetUsage.PlatformContents);
                fresh = true;

            } else {
                fresh = false;
            }

            lock (Entries) {
                Used.Add(rt);
                UsedMemory += rt.GetMemoryUsage();
                if (fresh)
                    TotalMemory += rt.GetMemoryUsage();
            }

            return new(this, rt, new(0, 0, rt.Width, rt.Height));
        }

        public void Free(RenderTarget2D? rt) {
            if (rt == null)
                return;
            lock (Entries) {
                Used.Remove(rt);
                UsedMemory -= rt.Width * rt.Height * 4;

                if (rt.IsDisposed)
                    return;

                if (CullTriggered) {
                    rt.Dispose();
                    TotalMemory -= rt.GetMemoryUsage();

                } else if (EntriesAlive >= Entries.Length) {
                    CullTriggered = true;
                    rt.Dispose();
                    TotalMemory -= rt.GetMemoryUsage();

                } else {
                    for (int i = 0; i < Entries.Length; i++) {
                        if (Entries[i].IsNull) {
                            Entries[i] = new(rt);
                            EntriesAlive++;
                            return;
                        }
                    }
                    // This shouldn't ever be reached but eh.
#if DEBUG
                    throw new Exception("This shouldn't ever be reached.");
#endif
                    CullTriggered = true;
                    rt.Dispose();
                    TotalMemory -= rt.GetMemoryUsage();
                }
            }
        }

        public struct Entry : ISizeable {

            public RenderTarget2D? RT;

            public int Width { get; set; }
            public int Height { get; set; }

            public int Age;

            [MemberNotNullWhen(false, nameof(RT))]
            public bool IsDisposed => RT?.IsDisposed ?? true;
            [MemberNotNullWhen(false, nameof(RT))]
            public bool IsNull => RT == null;

            public Entry(RenderTarget2D rt) {
                RT = rt;
                Width = rt.Width;
                Height = rt.Height;
                Age = 0;
            }

        }

    }
}
