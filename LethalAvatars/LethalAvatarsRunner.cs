using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using LethalAvatars.GameUI;
using LethalAvatars.Networking.Messages;
using UnityEngine;

namespace LethalAvatars;

public class LethalAvatarsRunner : MonoBehaviour
{
    public static LethalAvatarsRunner? Instance { get; private set; }

    public Coroutine RunCoroutine(IEnumerator c) => StartCoroutine(c);
    public void HaltCoroutine(IEnumerator c) => StopCoroutine(c);
    public void HaltCoroutine(Coroutine c) => StopCoroutine(c);
    
    private static readonly Queue<Action> _executionQueue = new();

	/// <summary>
	/// Locks the queue and adds the IEnumerator to the queue
	/// </summary>
	/// <param name="action">IEnumerator function that will be executed from the main thread.</param>
	public void Enqueue(IEnumerator action) {
		lock (_executionQueue) {
			_executionQueue.Enqueue (() => {
				StartCoroutine (action);
			});
		}
	}

	/// <summary>
	/// Locks the queue and adds the Action to the queue
	/// </summary>
	/// <param name="action">function that will be executed from the main thread.</param>
	public void Enqueue(Action action)
	{
		Enqueue(ActionWrapper(action));
	}

	/// <summary>
	/// Locks the queue and adds the Action to the queue, returning a Task which is completed when the action completes
	/// </summary>
	/// <param name="action">function that will be executed from the main thread.</param>
	/// <returns>A Task that can be awaited until the action completes</returns>
	public Task EnqueueAsync(Action action)
	{
		var tcs = new TaskCompletionSource<bool>();

		void WrappedAction() {
			try 
			{
				action();
				tcs.TrySetResult(true);
			} catch (Exception ex) 
			{
				tcs.TrySetException(ex);
			}
		}

		Enqueue(ActionWrapper(WrappedAction));
		return tcs.Task;
	}


	IEnumerator ActionWrapper(Action a)
	{
		a();
		yield return null;
	}

    private void Start()
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }
        DontDestroyOnLoad(gameObject);
        Instance = this;
    }

    private void Update()
    {
        AvatarData.Update();
        LoadingNameplate.Update();
        lock(_executionQueue) {
	        while (_executionQueue.Count > 0) {
		        _executionQueue.Dequeue().Invoke();
	        }
        }
    }
}