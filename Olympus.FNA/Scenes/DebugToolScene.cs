using OlympUI;
using System.Collections.ObjectModel;

namespace Olympus {
    public abstract partial class DebugToolSceneBase : Scene {
        public override Element Generate() 
            => new Group() {
                ID = "DebugToolScene",
                Layout = {
                    Layouts.Fill(1, 1),
                    Layouts.Column(),
                },
                Style = {
                    { Group.StyleKeys.Spacing, 8},
                    { Group.StyleKeys.Padding, 8},
                },
                Children = {
                    Section("Test scene", new() {
                        new NavButton("Open test scene", Scener.Get<TestScene>()),
                    }),
                    Section("WorkingOnIt Scene", new() {
                        new Button("Open WorkingOnItScene", b => {
                            Scener.PopFront();
                            WorkingOnItScene.Job job = WorkingOnItScene.GetDummyJob();
                            Scener.Set<WorkingOnItScene>(job, "download_rot");
                        }),
                        new Button("Open CRASH WorkingOnItScene", b => {
                            Scener.PopFront();
                            WorkingOnItScene.Job job = WorkingOnItScene.GetCrashyJob();
                            Scener.Set<WorkingOnItScene>(job, "download_rot");
                        }),
                    }),
                    Section("ManageInstalls Screen", new() {
                        new Button("Deselect current install", b => {
                            InstallManagerScene.SelectedInstall = null;
                            Config.Instance.Installation = null;
                            Config.Instance.Save();
                        }),
                    })
                }
                
            };

        private static Element Section(string title, ObservableCollection<Element> children) {
            return new Group() {
                Layout = {
                    Layouts.Column(),
                },
                Style = {
                    { Group.StyleKeys.Spacing, 8},
                },
                Children = {
                    new HeaderBig(title),
                    new Group() {
                        Layout = {
                            Layouts.Row(),
                        },
                        Style = {
                            { Group.StyleKeys.Spacing, 8},
                        },
                        Children = children,
                    }
                }
            };
        }
        
        public partial class NavButton : Button {
            private readonly Scene scene;

            public NavButton(string text, Scene scene) : base(text) {
                this.scene = scene;
                Callback = b => Scener.Set(((NavButton) b).scene);
            }
            
        }
        
    }

    public partial class DebugToolScene : DebugToolSceneBase {
        public override bool Alert => false;
    }

    public partial class DebugToolSceneAlert : DebugToolSceneBase {
        public override bool Alert => true;
    }
}