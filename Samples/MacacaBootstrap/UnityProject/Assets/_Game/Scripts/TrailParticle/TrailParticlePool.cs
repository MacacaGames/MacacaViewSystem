using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MacacaGames;
using System;

public class TrailParticlePool : UnitySingleton<TrailParticlePool>
{
    [SerializeField] bool ConstantHitParticlePosition = false;
    [SerializeField] Vector3 _moneyTextPoint = new Vector3(-2.75f, 4.9f, 0f);
    Vector3? _fixed_moneyTextPoint;

    Vector3 moneyTextPoint
    {
        get
        {
            if (_fixed_moneyTextPoint == null)
            {
                Vector3 fixedPoint = _moneyTextPoint;
                fixedPoint.x *= (float)Screen.width / Screen.height * 16f / 9f;
                _fixed_moneyTextPoint = fixedPoint;
            }

            return _fixed_moneyTextPoint.Value;
        }
    }

    public TrailParticle SpawnTrail(TrailType type, Vector3 spawnPos, Vector3 targetPos, Sprite sprite,
        Transform parent = null)
    {
        TrailSet set = trailSets[(int)type];
        TrailParticle tracePlayer = set.SpawnTrail(spawnPos, targetPos, sprite, parent);
        return tracePlayer;
    }

    public TrailParticle SpawnTrail(TrailType type, Vector3 spawnPos, Vector3 targetPos, Transform parent = null)
    {
        TrailSet set = trailSets[(int)type];
        TrailParticle tracePlayer = set.SpawnTrail(spawnPos, targetPos, parent);
        return tracePlayer;
    }

    public TrailParticle SpawnTrail(TrailType type, Vector3 spawnPos, Vector3 targetPos)
    {
        TrailSet set = trailSets[(int)type];
        TrailParticle tracePlayer = set.SpawnTrail(spawnPos, targetPos);
        return tracePlayer;
    }

    public TrailParticle SpawnTrail(TrailType type, Vector3 spawnPos, Transform targetTransform)
    {
        TrailSet set = trailSets[(int)type];
        TrailParticle tracePlayer = set.SpawnTrail(spawnPos, targetTransform);
        return tracePlayer;
    }

    // public TracePlayer SpawnTrail(TrailType type, Vector3 spawnPos)
    // {
    //     if (type == TrailType.Coin)
    //     {
    //         return SpawnTrail(type, spawnPos, moneyTextPoint);
    //     }
    //
    //     TrailSet set = trailSets[(int)type];
    //     TracePlayer tracePlayer = set.SpawnTrail(spawnPos);
    //     return tracePlayer;
    // }
    public enum TrailType
    {
        Coin,
        Bad,
        Good
    }

    [Header("0 - Coin     1 - Energy    2 - Blood")]
    public TrailSet[] trailSets;

    [Serializable]
    public struct TrailSet
    {
        public ObjectPool trailPool;
        public ParticleSystem hitPar;
        public ParticleSystem spawnOneShotParticle;
        public bool ConstantHitParticlePosition;

        public TrailSet(ObjectPool trailPool, ParticleSystem hitPar, ParticleSystem spawnOneShotParticle, bool ConstantHitParticlePosition)
        {
            this.trailPool = trailPool;
            this.hitPar = hitPar;
            this.spawnOneShotParticle = spawnOneShotParticle;
            this.ConstantHitParticlePosition = ConstantHitParticlePosition;
        }


        public TrailParticle SpawnTrail(Vector3 spawnPos, Vector3 targetPos, Sprite sprite, Transform parent = null,
            bool isAbsoluteSpeed = true)
        {
            TrailParticle tracePlayer = trailPool.ReUse<TrailParticle>(spawnPos, Quaternion.identity);
            tracePlayer.transformCache.SetParent(parent, false);
            tracePlayer.transformCache.position = spawnPos;
            tracePlayer.transformCache.localScale = Vector3.one;
            tracePlayer.gameObject.SetActive(true);
            tracePlayer.StartTrace(targetPos, TraceEnd, isAbsoluteSpeed);
            try
            {
                if (sprite != null) tracePlayer.GetComponent<UnityEngine.UI.Image>().sprite = sprite;
            }
            catch
            {
            }
            if (spawnOneShotParticle != null)
            {
                spawnOneShotParticle.transform.position = spawnPos;
                spawnOneShotParticle.Play();
            }

            return tracePlayer;
        }

        public TrailParticle SpawnTrail(Vector3 spawnPos, Vector3 targetPos, Transform parent = null)
        {
            TrailParticle tracePlayer = trailPool.ReUse<TrailParticle>(spawnPos, Quaternion.identity);
            tracePlayer.transformCache.SetParent(parent, false);
            tracePlayer.transformCache.position = spawnPos;
            tracePlayer.transformCache.localScale = Vector3.one;
            tracePlayer.gameObject.SetActive(true);
            tracePlayer.StartTrace(targetPos, TraceEnd);
            if (spawnOneShotParticle != null)
            {
                spawnOneShotParticle.transform.position = spawnPos;
                spawnOneShotParticle.Play();
            }
            return tracePlayer;
        }

        public TrailParticle SpawnTrail(Vector3 spawnPos, Transform target, Transform parent = null)
        {
            TrailParticle tracePlayer = trailPool.ReUse<TrailParticle>(spawnPos, Quaternion.identity);
            tracePlayer.transformCache.SetParent(parent, false);
            tracePlayer.transformCache.position = spawnPos;
            tracePlayer.transformCache.localScale = Vector3.one;
            tracePlayer.gameObject.SetActive(true);
            tracePlayer.StartTrace(target, TraceEnd);
            if (spawnOneShotParticle != null)
            {
                spawnOneShotParticle.transform.position = spawnPos;
                spawnOneShotParticle.Play();
            }
            return tracePlayer;
        }

        void TraceEnd(GameObject obj, Vector3 targetPos)
        {
            if (hitPar != null)
            {
                if (!ConstantHitParticlePosition)
                    hitPar.transform.position = targetPos;

                hitPar.Play();
            }

            trailPool.Recovery(obj);
        }
    }
}