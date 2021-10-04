﻿using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OlympUI;
using Olympus.NativeImpls;
using SDL2;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using static Olympus.NativeImpls.NativeImpl;

namespace Olympus {
    public class App : Game {

#pragma warning disable CS8618 // Nullability is fun but can't see control flow.
        public static App Instance;
#pragma warning restore CS8618

        public static readonly object[] EmptyArgs = new object[0];

        public static readonly MethodInfo m_GameWindow_OnClientSizeChanged =
            typeof(GameWindow).GetMethod("OnClientSizeChanged", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance) ??
            throw new Exception($"GameWindow.OnClientSizeChanged not found!");


        public GraphicsDeviceManager Graphics;
        public SpriteBatch SpriteBatch;


        public readonly Stopwatch GlobalWatch = Stopwatch.StartNew();
        public float Time => (float) GlobalWatch.Elapsed.TotalSeconds;


        private Rectangle PrevClientBounds = new();

        private RenderTarget2D? FakeBackbuffer;


        private uint DrawCount = 0;


        public int FPS;
        private int CountingFrames;
        private TimeSpan CountingFramesTime = new(0);
        private long CountingFramesWatchLast;

        public bool Resizing;
        public bool ManualUpdate;
        private bool ManuallyUpdated = true;
        public bool ManualUpdateSkip;

        private readonly Dictionary<Type, object> ComponentCache = new();


        private int? WidthOverride;
        public int Width => WidthOverride ?? GraphicsDevice.PresentationParameters.BackBufferWidth;
        private int? HeightOverride;
        public int Height => HeightOverride ?? GraphicsDevice.PresentationParameters.BackBufferHeight;


        public float BackgroundOpacityTime = 0f;

        public bool VSync = false; // FIXME: DON'T SHIP WITH VSYNC OFF!

        
#pragma warning disable CS8618 // Nullability is fun but can't see control flow.
        public App() {
#pragma warning restore CS8618
            Instance = this;

            Graphics = new GraphicsDeviceManager(this);
            Graphics.PreferredDepthStencilFormat = DepthFormat.None;
            Graphics.PreferMultiSampling = false;
            Graphics.PreferredBackBufferWidth = 1100;
            Graphics.PreferredBackBufferHeight = 600;
            SDL.SDL_SetWindowMinimumSize(Window.Handle, 800, 600);

#if DEBUG
            Window.Title = "Olympus.FNA (DEBUG)";
#else
            Window.Title = "Olympus.FNA";
#endif
            Window.AllowUserResizing = true;
            IsMouseVisible = true;

            IsFixedTimeStep = false;
            Graphics.SynchronizeWithVerticalRetrace = true;

            Content.RootDirectory = "Content";

#if WINDOWS
            Native = new NativeWin32(this);
#else
            Native = new NativeNop(this);
#endif
        }


        public T Get<T>() where T : AppComponent {
            if (ComponentCache.TryGetValue(typeof(T), out object? value))
                return (T) value;
            foreach (IGameComponent component in Components)
                if (component is T)
                    return (T) (ComponentCache[typeof(T)] = component);
            throw new Exception($"App component of type \"{typeof(T).FullName}\" not found");
        }


        protected override void Initialize() {
            Components.Add(new OverlayComponent(this));
            if (Native.SplashSize != default)
                Components.Add(new SplashComponent(this));
            Components.Add(new MainComponent(this));

            base.Initialize();
        }


        protected override void LoadContent() {
            UI.LoadContent();

            SpriteBatch?.Dispose();
            SpriteBatch = new SpriteBatch(GraphicsDevice);

            base.LoadContent();
        }


        protected override void Update(GameTime gameTime) {
            Resizing = false;
            if (ManualUpdate) {
                ManualUpdate = false;
                // FNA has accumulated the elapsed game time for us, turning it unusuable.
                // Let's skip updating properly on one frame instead of having a huge delta time spike...
                gameTime = new();
            }

            if (DrawCount == 1) {
                // This MUST happen immediately after the first update + draw + present!
                // Otherwise we risk flickering.
                Native.PrepareLate();
                // We can update multiple times before a draw.
                DrawCount++;
            }

            IsFixedTimeStep = !Native.IsActive;

            Native.Update((float) gameTime.ElapsedGameTime.TotalSeconds);

            base.Update(gameTime);

            // SuppressDraw();
        }


        protected override void Draw(GameTime gameTime) {
            TimeSpan dtSpan = gameTime.ElapsedGameTime;
            if (dtSpan.Ticks == 0) {
                // User resized the window and FNA doesn't keep track of elapsed time.
                dtSpan = new(GlobalWatch.ElapsedTicks - CountingFramesWatchLast);
                gameTime = new(gameTime.TotalGameTime, dtSpan, gameTime.IsRunningSlowly);
                Resizing = true;
                ManualUpdate = true;
                ManuallyUpdated = true;
            }
            CountingFramesTime += dtSpan;
            CountingFramesWatchLast = GlobalWatch.ElapsedTicks;
            float dt = gameTime.GetDeltaTime();

            Rectangle clientBounds = Window.ClientBounds;

            Resizing &= PrevClientBounds != clientBounds && PrevClientBounds != default;
            if (PrevClientBounds != clientBounds) {
                // FNA resizes the client bounds, but not the backbuffer.
                // Let's help out by enforcing a backbuffer resize in an ugly way.
                // Also, disable forced vsync for smooth resizing, re-enable on next update.

                if (Graphics.SynchronizeWithVerticalRetrace) {
                    Graphics.SynchronizeWithVerticalRetrace = false;
                    Graphics.GraphicsDevice.PresentationParameters.PresentationInterval = PresentInterval.Immediate;
                }

                // Apparently some dual GPU setups can experience severe flickering when resizing the backbuffer repeatedly.
                if (PrevClientBounds.Width != clientBounds.Width ||
                    PrevClientBounds.Height != clientBounds.Height
                ) {
                    if (!Native.ReduceBackBufferResizes) {
                        WidthOverride = HeightOverride = null;
                        m_GameWindow_OnClientSizeChanged.Invoke(Window, EmptyArgs);
                    } else {
                        WidthOverride = clientBounds.Width;
                        HeightOverride = clientBounds.Height;
                        if (GraphicsDevice.PresentationParameters is PresentationParameters pp && (
                            pp.BackBufferWidth < Width ||
                            pp.BackBufferHeight < Height
                        )) {
                            pp.BackBufferWidth = Math.Max(pp.BackBufferWidth, Width + 256);
                            pp.BackBufferHeight = Math.Max(pp.BackBufferHeight, Height + 256);
                            GraphicsDevice.Reset(pp);
                        }
                    }
                }

                PrevClientBounds = clientBounds;

            } else if (ManuallyUpdated && !ManualUpdate) {
                ManuallyUpdated = false;

                if (!Graphics.SynchronizeWithVerticalRetrace && VSync) {
                    Graphics.SynchronizeWithVerticalRetrace = true;
                    Graphics.GraphicsDevice.PresentationParameters.PresentationInterval = PresentInterval.One;
                }

                // XNA - and thus in turn FNA - love to re-center the window on device changes.
                Point pos = Native.WindowPosition;
                FNAPatches.ApplyWindowChangesWithoutCenter = true;
                m_GameWindow_OnClientSizeChanged.Invoke(Window, EmptyArgs);
                FNAPatches.ApplyWindowChangesWithoutCenter = false;

                // In some circumstances, fixing the window position is required, but only on device changes.
                if (Native.WindowPosition != pos)
                    Native.WindowPosition = Native.FixWindowPositionDisplayDrag(pos);

                WidthOverride = HeightOverride = null;
            }

            if (CountingFramesTime.Ticks >= TimeSpan.TicksPerSecond) {
                CountingFramesTime = new TimeSpan(CountingFramesTime.Ticks % TimeSpan.TicksPerSecond);
                FPS = CountingFrames;
                CountingFrames = 0;
            }
            CountingFrames++;

            if (!Native.CanRenderTransparentBackground) {
                BackgroundOpacityTime = 2.4f;
            } else if (Native.IsActive) {
                BackgroundOpacityTime -= dt * 4f;
                if (BackgroundOpacityTime < 0f)
                    BackgroundOpacityTime = 0f;
            } else {
                BackgroundOpacityTime += dt * 4f;
                if (BackgroundOpacityTime > 1f)
                    BackgroundOpacityTime = 1f;
            }

            if (FakeBackbuffer != null && (FakeBackbuffer.Width < Width || FakeBackbuffer.Height < Height)) {
                FakeBackbuffer.Dispose();
                FakeBackbuffer = null;
            }

            if (FakeBackbuffer == null || FakeBackbuffer.IsDisposed)
                FakeBackbuffer = new RenderTarget2D(GraphicsDevice, Width, Height, false, SurfaceFormat.Color, DepthFormat.None, UI.MultiSampleCount, RenderTargetUsage.PreserveContents);
            GraphicsDevice.SetRenderTarget(FakeBackbuffer);
            GraphicsDevice.Viewport = new(0, 0, Width, Height);
            GraphicsDevice.Clear(new Color(0f, 0f, 0f, 0f));
            Native.BeginDrawRT(dt);

            // FIXME: This should be in a better spot, but Native can edit Viewport which UI relies on and ugh.
            if (ManualUpdate) {
                if (ManualUpdateSkip) {
                    ManualUpdateSkip = false;
                } else {
                    base.Update(gameTime);
                }
            }

            base.Draw(gameTime);
            Native.EndDrawRT(dt);

            GraphicsDevice.SetRenderTarget(null);
            if (Native.CanRenderTransparentBackground) {
                GraphicsDevice.Clear(new Color(0f, 0f, 0f, 0f));
            } else if (Native.DarkMode) {
                GraphicsDevice.Clear(new Color(0.1f, 0.1f, 0.1f, 1f));
            } else {
                GraphicsDevice.Clear(new Color(0.9f, 0.9f, 0.9f, 1f));
            }
            Native.BeginDrawBB(dt);
            Viewport viewBB = GraphicsDevice.Viewport;
            SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, UI.RasterizerStateCullCounterClockwiseScissoredNoMSAA);
            SpriteBatch.Draw(
                FakeBackbuffer,
                new Rectangle(0, 0, viewBB.Width, viewBB.Height),
                new Rectangle(0, 0, Width, Height),
                new Color(1f, 1f, 1f, 1f)
            );
            SpriteBatch.End();
            Native.EndDrawBB(dt);

            DrawCount++;
        }

    }
}
