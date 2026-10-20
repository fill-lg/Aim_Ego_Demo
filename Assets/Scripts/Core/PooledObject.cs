using UnityEngine;
using System.Collections;

/// <summary>
/// Вспомогательный скрипт для объектов из пула, которые должны вернуться обратно через время.
/// </summary>
public class PooledObject : MonoBehaviour
{
    private System.Action<GameObject> _returnAction;

    public void Initialize(System.Action<GameObject> returnAction, float lifetime)
    {
        _returnAction = returnAction;
        StartCoroutine(ReturnAfterDelay(lifetime));
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_returnAction != null)
        {
            _returnAction(gameObject);
        }
    }
}
