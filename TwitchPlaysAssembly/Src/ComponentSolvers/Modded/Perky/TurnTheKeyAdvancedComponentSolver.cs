﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class TurnTheKeyAdvancedComponentSolver : ComponentSolver
{
	public TurnTheKeyAdvancedComponentSolver(TwitchModule module) :
		base(module)
	{
		_leftKey = (MonoBehaviour) LeftKeyField.GetValue(module.BombComponent.GetComponent(ComponentType));
		_rightKey = (MonoBehaviour) RightKeyField.GetValue(module.BombComponent.GetComponent(ComponentType));
		ModInfo = ComponentSolverFactory.GetModuleInfo(GetModuleType(), "Turn the left key with !{0} turn left. Turn the right key with !{0} turn right.");

		((KMSelectable) _leftKey).OnInteract = () => HandleKey(LeftBeforeA, LeftAfterA, LeftKeyTurnedField, RightKeyTurnedField, BeforeLeftKeyField, OnLeftKeyTurnMethod, LeftKeyAnimatorField);
		((KMSelectable) _rightKey).OnInteract = () => HandleKey(RightBeforeA, RightAfterA, RightKeyTurnedField, LeftKeyTurnedField, BeforeRightKeyField, OnRightKeyTurnMethod, RightKeyAnimatorField);
	}

	private bool HandleKey(string[] modulesBefore, IEnumerable<string> modulesAfter, FieldInfo keyTurned, FieldInfo otherKeyTurned, FieldInfo beforeKeyField, MethodInfo onKeyTurn, FieldInfo animatorField)
	{
		if (!GetValue(ActivatedField) || GetValue(keyTurned)) return false;
		KMBombInfo bombInfo = Module.GetComponent<KMBombInfo>();
		KMBombModule bombModule = Module.GetComponent<KMBombModule>();
		KMAudio bombAudio = Module.GetComponent<KMAudio>();
		Animator keyAnimator = (Animator) animatorField.GetValue(Module.GetComponent(ComponentType));

		if (TwitchPlaySettings.data.EnforceSolveAllBeforeTurningKeys &&
			modulesAfter.Any(x => bombInfo.GetSolvedModuleNames().Count(x.Equals) != bombInfo.GetSolvableModuleNames().Count(x.Equals)))
		{
			keyAnimator.SetTrigger("WrongTurn");
			bombAudio.PlaySoundAtTransform("WrongKeyTurnFK", Module.transform);
			bombModule.HandleStrike();
			return false;
		}

		beforeKeyField.SetValue(null, TwitchPlaySettings.data.DisableTurnTheKeysSoftLock ? new string[0] : modulesBefore);
		onKeyTurn.Invoke(Module.GetComponent(ComponentType), null);
		if (GetValue(keyTurned))
		{
			//Check to see if any forbidden modules for this key were solved.
			if (TwitchPlaySettings.data.DisableTurnTheKeysSoftLock && bombInfo.GetSolvedModuleNames().Any(modulesBefore.Contains))
				bombModule.HandleStrike(); //If so, Award a strike for it.

			if (!GetValue(otherKeyTurned)) return false;
			int modules = bombInfo.GetSolvedModuleNames().Count(x => RightAfterA.Contains(x) || LeftAfterA.Contains(x));
			TwitchPlaySettings.AddRewardBonus(2 * modules);
			IRCConnection.SendMessage("Reward increased by {0} for defusing module !{1} ({2}).", modules * 2, Code, bombModule.ModuleDisplayName);
		}
		else
		{
			keyAnimator.SetTrigger("WrongTurn");
			bombAudio.PlaySoundAtTransform("WrongKeyTurnFK", Module.transform);
		}
		return false;
	}

	protected override IEnumerator ForcedSolveIEnumerator()
	{
		yield return null;
		Component self = Module.GetComponent(ComponentType);
		Animator leftKeyAnimator = (Animator) LeftKeyAnimatorField.GetValue(self);
		Animator rightKeyAnimator = (Animator) RightKeyAnimatorField.GetValue(self);

		LeftKeyTurnedField.SetValue(self, true);
		RightKeyTurnedField.SetValue(self, true);
		rightKeyAnimator.SetBool("IsUnlocked", true);
		Module.GetComponent<KMAudio>().PlaySoundAtTransform("TurnTheKeyFX", Module.transform);
		yield return new WaitForSeconds(0.1f);
		leftKeyAnimator.SetBool("IsUnlocked", true);
		Module.GetComponent<KMAudio>().PlaySoundAtTransform("TurnTheKeyFX", Module.transform);
		yield return new WaitForSeconds(0.1f);
		Module.GetComponent<KMBombModule>().HandlePass();
	}

	private bool GetValue(FieldInfo field) => (bool) field.GetValue(Module.GetComponent(ComponentType));

	protected internal override IEnumerator RespondToCommandInternal(string inputCommand)
	{
		string[] commands = inputCommand.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

		if (commands.Length != 2 || commands[0] != "turn")
			yield break;

		MonoBehaviour key;
		switch (commands[1])
		{
			case "l":
			case "left":
				key = _leftKey;
				break;
			case "r":
			case "right":
				key = _rightKey;
				break;
			default:
				yield break;
		}
		yield return "Turning the key";
		yield return DoInteractionClick(key);
	}

	static TurnTheKeyAdvancedComponentSolver()
	{
		ComponentType = ReflectionHelper.FindType("TurnKeyAdvancedModule");
		LeftKeyField = ComponentType.GetField("LeftKey", BindingFlags.Public | BindingFlags.Instance);
		RightKeyField = ComponentType.GetField("RightKey", BindingFlags.Public | BindingFlags.Instance);
		ActivatedField = ComponentType.GetField("bActivated", BindingFlags.NonPublic | BindingFlags.Instance);
		BeforeLeftKeyField = ComponentType.GetField("LeftBeforeA", BindingFlags.NonPublic | BindingFlags.Static);
		BeforeRightKeyField = ComponentType.GetField("RightBeforeA", BindingFlags.NonPublic | BindingFlags.Static);
		AfterLeftKeyField = ComponentType.GetField("LeftAfterA", BindingFlags.NonPublic | BindingFlags.Static);
		AfterLeftKeyField?.SetValue(null, LeftAfterA);
		LeftKeyTurnedField = ComponentType.GetField("bLeftKeyTurned", BindingFlags.NonPublic | BindingFlags.Instance);
		RightKeyTurnedField = ComponentType.GetField("bRightKeyTurned", BindingFlags.NonPublic | BindingFlags.Instance);
		OnLeftKeyTurnMethod = ComponentType.GetMethod("OnLeftKeyTurn", BindingFlags.NonPublic | BindingFlags.Instance);
		OnRightKeyTurnMethod = ComponentType.GetMethod("OnRightKeyTurn", BindingFlags.NonPublic | BindingFlags.Instance);
		RightKeyAnimatorField = ComponentType.GetField("RightKeyAnim", BindingFlags.Public | BindingFlags.Instance);
		LeftKeyAnimatorField = ComponentType.GetField("LeftKeyAnim", BindingFlags.Public | BindingFlags.Instance);
	}

	private static readonly Type ComponentType;
	private static readonly FieldInfo LeftKeyField;
	private static readonly FieldInfo RightKeyField;
	private static readonly FieldInfo ActivatedField;
	private static readonly FieldInfo BeforeLeftKeyField;
	private static readonly FieldInfo BeforeRightKeyField;
	// ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
	private static readonly FieldInfo AfterLeftKeyField;
	private static readonly FieldInfo LeftKeyTurnedField;
	private static readonly FieldInfo RightKeyTurnedField;
	private static readonly FieldInfo RightKeyAnimatorField;
	private static readonly FieldInfo LeftKeyAnimatorField;

	private static readonly MethodInfo OnLeftKeyTurnMethod;
	private static readonly MethodInfo OnRightKeyTurnMethod;

	private readonly MonoBehaviour _leftKey;
	private readonly MonoBehaviour _rightKey;

	private static readonly string[] LeftAfterA = {
		"Password",
		"Crazy Talk",
		"Who's on First",
		"Keypad",
		"Listening",
		"Orientation Cube"
	};

	private static readonly string[] LeftBeforeA = {
		"Maze",
		"Memory",
		"Complicated Wires",
		"Wire Sequence",
		"Cryptography"
	};

	private static readonly string[] RightAfterA = {
		"Morse Code",
		"Wires",
		"Two Bits",
		"The Button",
		"Colour Flash",
		"Round Keypad"
	};

	private static readonly string[] RightBeforeA = {
		"Semaphore",
		"Combination Lock",
		"Simon Says",
		"Astrology",
		"Switches",
		"Plumbing"
	};
}
