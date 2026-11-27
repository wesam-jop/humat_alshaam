using System.Collections;
using Adventure3D.Configuration;
using Adventure3D.Core;
using Adventure3D.Input;
using Adventure3D.Platform;
using Adventure3D.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Adventure3D.Tests
{
    /// <summary>
    /// Device input through the real Input System with virtual devices (InputTestFixture). This is
    /// simulation of keyboard / gamepad / touchscreen in the editor, not a test on a physical phone.
    /// </summary>
    public class InputDeviceTests : InputTestFixture
    {
        LocalPlayerInput _input;
        GameConfig _cfg;

        LocalPlayerInput CreateInput()
        {
            _cfg = GameConfig.Load().CloneForRuntime();
            _input = new GameObject("TestInput").AddComponent<LocalPlayerInput>();
            _input.Configure(_cfg.Input, _cfg.Camera);
            return _input;
        }

        public override void Setup()
        {
            base.Setup();
            // Batch-mode editor has no focused Game view: route keyboard / pointer input anyway.
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            // The fixture's test runtime is internal; give the virtual player focus through reflection.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var rt = typeof(InputTestFixture).GetProperty("runtime", flags)?.GetValue(this);
            rt?.GetType().GetProperty("runInBackground", flags)?.SetValue(rt, true);
            rt?.GetType().GetMethod("PlayerFocusGained", flags)?.Invoke(rt, null);
        }

        public override void TearDown()
        {
            if (_input != null) Object.DestroyImmediate(_input.gameObject);
            PlatformInfo.ForceTouchUi = false;
            base.TearDown();
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        // Press() on keyboard keys does not reach the device in a batch-mode editor; full keyboard state
        // events do, and they go through the same device path as a real keyboard.
        static void Keys(Keyboard kb, params Key[] keys)
        {
            InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator KeyboardMovesSprintsAndLatchesJump()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            CreateInput();
            Keys(kb, Key.W, Key.LeftShift);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.IsTrue(kb.wKey.isPressed, "W reads as pressed");
            var cmd = _input.SampleCommand();
            Assert.AreEqual(1f, cmd.Move.y, 0.01f, "W moves forward after the keyboard response time");
            Assert.IsTrue(cmd.Sprint, "Shift sprints");

            // Queue only: the event is processed by the frame's own input update, like a real key press.
            InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.W, Key.LeftShift, Key.Space));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.W, Key.LeftShift));
            yield return null;
            yield return null;
            Assert.IsTrue(_input.SampleCommand().JumpPressed, "jump press is kept until the next tick samples it");
            Assert.IsFalse(_input.SampleCommand().JumpPressed, "and consumed exactly once");

            Keys(kb, Key.D);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.AreEqual(1f, _input.SampleCommand().Move.x, 0.01f, "D strafes right");
            Keys(kb);
        }
        [UnityTest]
        public IEnumerator GamepadDeadzoneMoveAndLook()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            CreateInput();
            Set(pad.leftStick, new Vector2(0.08f, 0f));
            yield return Frames(2);
            Assert.AreEqual(Vector2.zero, _input.SampleCommand().Move, "inside the dead zone");

            Set(pad.leftStick, new Vector2(0f, 1f));
            yield return Frames(2);
            Assert.AreEqual(1f, _input.SampleCommand().Move.y, 0.01f);

            float yaw0 = _input.LookYaw;
            Set(pad.rightStick, new Vector2(1f, 0f));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.Greater(Mathf.DeltaAngle(yaw0, _input.LookYaw), 5f, "right stick turns");
            Press(pad.buttonSouth);
            yield return Frames(1);
            Assert.IsTrue(_input.SampleCommand().JumpPressed);
            Release(pad.buttonSouth);
            Set(pad.leftStick, Vector2.zero);
            Set(pad.rightStick, Vector2.zero);
        }

        [UnityTest]
        public IEnumerator MultiTouchMovesLooksAndJumpsAtTheSameTime()
        {
            InputSystem.AddDevice<Touchscreen>();
            PlatformInfo.ForceTouchUi = true;
            CreateInput();
            var safe = new Rect(0, 0, 1920, 1080);
            _input.SafeAreaOverride = safe;
            Vector2 P(float nx, float ny) => new Vector2(safe.x + nx * safe.width, safe.y + ny * safe.height);

            BeginTouch(1, P(0.2f, 0.3f));
            BeginTouch(2, P(0.6f, 0.5f));
            yield return Frames(1);
            MoveTouch(1, P(0.2f, 0.3f) + new Vector2(safe.height * 0.09f, 0f));
            MoveTouch(2, P(0.65f, 0.5f));
            BeginTouch(3, P(0.9f, 0.18f)); // jump button
            yield return new WaitForSecondsRealtime(0.1f);

            float yaw0 = 0f;
            Assert.AreEqual(3, _input.Touch.ActiveFingerCount);
            Assert.Greater(_input.Touch.Move.x, 0.5f, "stick finger moves right");
            Assert.Greater(Mathf.DeltaAngle(yaw0, _input.LookYaw), 0.5f, "look finger turns");
            var cmd = _input.SampleCommand();
            Assert.IsTrue(cmd.JumpPressed, "jump at the same time");
            Assert.Greater(cmd.Move.x, 0.3f);

            EndTouch(3, P(0.9f, 0.18f));
            EndTouch(2, P(0.65f, 0.5f));
            EndTouch(1, P(0.3f, 0.3f));
            yield return new WaitForSecondsRealtime(0.1f);
            Debug.Log($"[A3D-TEST] touch fingers={_input.Touch.ActiveFingerCount} touchMove={_input.Touch.Move} current={_input.CurrentMove} crouch={_input.CrouchActive}");
            Assert.AreEqual(Vector2.zero, _input.SampleCommand().Move);
        }

        [UnityTest]
        public IEnumerator FocusLossReleasesHeldSprintAndStick()
        {
            InputSystem.AddDevice<Touchscreen>();
            PlatformInfo.ForceTouchUi = true;
            CreateInput();
            var safe = new Rect(0, 0, 1920, 1080);
            _input.SafeAreaOverride = safe;
            BeginTouch(1, new Vector2(safe.x + 0.76f * safe.width, safe.y + 0.18f * safe.height)); // sprint
            BeginTouch(2, new Vector2(safe.x + 0.2f * safe.width, safe.y + 0.3f * safe.height));   // stick
            yield return Frames(1);
            Assert.IsTrue(_input.SprintActive);
            AppFocus.Raise(false);
            Assert.IsFalse(_input.SprintActive, "sprint must not stay pressed after focus loss");
            Assert.AreEqual(Vector2.zero, _input.CurrentMove);
            Assert.AreEqual(0, _input.Touch.ActiveFingerCount);
            AppFocus.Raise(true);
            EndTouch(1, Vector2.zero);
            EndTouch(2, Vector2.zero);
        }

        [UnityTest]
        public IEnumerator DisabledInputProducesNeutralCommands()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            CreateInput();
            Keys(kb, Key.W);
            yield return new WaitForSecondsRealtime(0.1f);
            _input.InputEnabled = false; // network pause menu
            yield return Frames(2);
            var cmd = _input.SampleCommand();
            Assert.AreEqual(Vector2.zero, cmd.Move);
            Assert.AreEqual(CommandButtons.None, cmd.Buttons);
            Keys(kb);
        }
    }

    /// <summary>Main menu with a (virtual) gamepad: first item selected, navigation, back, and focus restore after a dialog.</summary>
    public class MenuNavigationTests : InputTestFixture
    {
        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        IEnumerator Tap(ButtonControl control)
        {
            Press(control);
            yield return Frames(2);
            Release(control);
            yield return Frames(2);
        }

        static GameObject Selected => EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        [UnityTest]
        public IEnumerator GamepadCanDriveTheWholeMenu()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            MenuServices.Current = null;
            SceneManager.LoadScene("MainMenu");
            yield return Frames(5);
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.IsNotNull(menu);
            Assert.AreEqual(menu.FirstMainButton.gameObject, Selected, "first item is selected when the screen opens");

            yield return Tap(pad.dpad.down);
            Assert.AreEqual(menu.MultiplayerButton.gameObject, Selected, "d-pad moves the selection");
            yield return Tap(pad.buttonSouth);
            Assert.IsTrue(menu.MultiplayerPanelOpen, "A / Cross opens multiplayer");
            yield return Tap(pad.dpad.down);
            Assert.AreEqual(menu.JoinButton.gameObject, Selected);

            yield return Tap(pad.buttonSouth);
            Assert.IsTrue(menu.JoinPanelOpen, "A / Cross opens the join screen");
            Assert.AreEqual(menu.ConnectButton.gameObject, Selected, "join screen focuses Connect");

            yield return Tap(pad.buttonEast);
            Assert.IsFalse(menu.JoinPanelOpen, "B / Circle goes back");
            Assert.IsTrue(menu.MultiplayerPanelOpen);
            yield return Tap(pad.buttonEast);
            Assert.AreEqual(menu.FirstMainButton.gameObject, Selected, "back to the main items");

            menu.OpenJoinLan();
            yield return Frames(2);
            menu.AddressField.text = "bad address!";
            yield return Tap(pad.buttonSouth); // Connect with invalid address
            Assert.IsTrue(menu.Dialog.IsOpen, "Arabic error dialog");
            Assert.AreEqual(menu.Dialog.OkButton.gameObject, Selected, "dialog takes focus");
            StringAssert.Contains("غير صالح", menu.Dialog.BodyLogical);

            yield return Tap(pad.buttonSouth);
            Assert.IsFalse(menu.Dialog.IsOpen);
            Assert.AreEqual(menu.ConnectButton.gameObject, Selected, "focus returns to Connect after the dialog");
        }
    }
}
