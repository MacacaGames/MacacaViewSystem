using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrailParticle : TransformCacheBase
{
    [SerializeField]
    Vector2 explodeForceRandomRange;
    [SerializeField]
    float acc_ad;
    [SerializeField]
    float acc;

    float currentAcc;

    [SerializeField]
    bool is2D = false;

    public void StartTrace(Transform target, System.Action<GameObject, Vector3> onEnd)
    {
        CoroutineManager.Instance.StartCoroutine(TraceTask(target, onEnd));
    }

    public void StartTrace(Vector3 target, System.Action<GameObject, Vector3> onEnd, bool isRelitiveSpeed = false)
    {
        CoroutineManager.Instance.StartCoroutine(TraceTask(target, onEnd, isRelitiveSpeed));
    }

    [SerializeField]
    float destroyRange = .5f;
    [SerializeField, Range(0, 1)]
    float smoothFactor = .1f;
    IEnumerator TraceTask(Transform targetTransform, System.Action<GameObject,Vector3> onEnd)
    {
        bool? face = null;
        Vector3 delta;
        Vector3 v = Random.onUnitSphere * Random.Range(explodeForceRandomRange.x, explodeForceRandomRange.y);
        v.z = 0;
        currentAcc = acc;
        do
        {
            if (targetTransform == null)
                break;

            delta = targetTransform.position - transformCache.position;
            delta.z = 0;

            currentAcc += MacacaGames.GlobalTimer.deltaTime * acc_ad;
            v += MacacaGames.GlobalTimer.deltaTime * currentAcc * delta.normalized;
            v *= 1 - smoothFactor;
            transformCache.position += v;

            bool _face = Mathf.Sign(delta.x) > 0;
            if (face == null)
                face = _face;
            if (_face != face)
            {
                if (delta.magnitude < destroyRange)
                    break;
                else
                    face = _face;
            }

            yield return null;
        }
        while (delta.magnitude > v.magnitude && delta.magnitude > destroyRange && targetTransform != null);

        if (targetTransform != null)
            transformCache.position = targetTransform.position;

        yield return null;

        onEnd(gameObject, targetTransform.position);
    }

    IEnumerator TraceTask(Vector3 targetPos, System.Action<GameObject,Vector3> onEnd,bool isRelitiveSpeed = false)
    {
        float l = isRelitiveSpeed ? Screen.height: 1;

        bool? face = null;
        Vector3 delta;
        Vector3 v = Random.onUnitSphere * Random.Range(explodeForceRandomRange.x, explodeForceRandomRange.y) * l;
        if (is2D)
        {
            v = Random.insideUnitCircle.normalized * Random.Range(explodeForceRandomRange.x, explodeForceRandomRange.y) * l;
            v.z = 0;
        }

        currentAcc = acc*l;
        do
        {
            if (targetPos == null)
                break;

            delta = targetPos - transformCache.position;
            delta.z = 0;
            currentAcc += MacacaGames.GlobalTimer.deltaTime * acc_ad*l;
            v += MacacaGames.GlobalTimer.deltaTime * currentAcc * delta.normalized;
            v *= Mathf.Pow(1 - smoothFactor, MacacaGames.GlobalTimer.deltaTime * 60);
            if (is2D)
                v.z = 0;
            transformCache.position += v;

            bool _face = Mathf.Sign(delta.x) > 0;
            if (face == null)
                face = _face;
            if (_face != face)
            {
                if (delta.magnitude < destroyRange*l)
                    break;
                else
                    face = _face;
            }

            yield return null;
        }
        while (delta.magnitude > v.magnitude && delta.magnitude > destroyRange && targetPos != null);

        transformCache.position = targetPos;

        yield return null;

        onEnd(gameObject, targetPos);
    }
}
