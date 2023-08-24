﻿
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class RepositionManager : UdonSharpBehaviour
{
    private const float k_BALL_DSQR = 0.0036f;
    private const float k_BALL_RADIUS = 0.03f;

    [SerializeField] private Transform snookerCircle;

    private BilliardsModule table;

    private int repositionCount;
    private bool[] repositioning;

    public void _Init(BilliardsModule table_)
    {
        table = table_;

        repositioning = new bool[table.balls.Length];

        _OnGameStarted();
    }

    public void _OnGameStarted()
    {
        repositionCount = 0;
        table.isBreak = true;
        Array.Clear(repositioning, 0, repositioning.Length);
    }

    public void _Tick()
    {
        if (repositionCount == 0) return;

        Vector3 k_pR = (Vector3)table.currentPhysicsManager.GetProgramVariable("k_pR");
        Vector3 k_pO = (Vector3)table.currentPhysicsManager.GetProgramVariable("k_pO");
        Transform transformSurface = (Transform)table.currentPhysicsManager.GetProgramVariable("transform_Surface");
        for (int i = 0; i < repositioning.Length; i++)
        {
            if (!repositioning[i]) continue;
            if (i > 0 && !table.isPracticeMode && !table._IsLocalPlayerReferee()) continue;

            GameObject ball = table.balls[i];

            Transform pickupTransform = ball.transform.GetChild(0);

            float maxX;
            if (table.isPracticeMode)
            {
                maxX = k_pR.x;
            }
            else if (i != 0)
            {
                maxX = k_pR.x;
            }
            else if (table._IsLocalPlayerReferee())
            {
                maxX = k_pR.x;
            }
            else
            {
                maxX = table.repoMaxX;
            }

            Vector3 boundedLocation = table.transform.InverseTransformPoint(pickupTransform.position);
            boundedLocation.x = Mathf.Clamp(boundedLocation.x, -k_pR.x, maxX);
            boundedLocation.z = Mathf.Clamp(boundedLocation.z, -k_pO.z, k_pO.z);
            boundedLocation.y = 0.0f;

            // ensure no collisions
            bool collides = false;
            Collider[] colliders = Physics.OverlapSphere(transformSurface.TransformPoint(boundedLocation), k_BALL_RADIUS);
            for (int j = 0; j < colliders.Length; j++)
            {
                if (colliders[j] == null) continue;

                GameObject collided = colliders[j].gameObject;
                if (collided == ball) continue;

                int collidedBall = Array.IndexOf(table.balls, collided);
                if (collidedBall != -1)
                {
                    collides = true;
                    break;
                }

                if (collided.name == "table" || collided.name == "glass" || collided.name == ".4BALL_FILL")
                {
                    collides = true;
                    break;
                }
            }

            if (!collides)
            {
                if (table.isSnooker6Red && i == 0 && table.isBreak)
                {
                    Vector3 snookerCircleCenter = snookerCircle.transform.localPosition;
                    float radius = 0.24f;

                    bool isNewLocationInCircle = IsInSemiCircle(boundedLocation, snookerCircleCenter, radius);
                    bool isCurrentLocationInCircle = IsInSemiCircle(table.ballsP[i], snookerCircleCenter, radius);

                    boundedLocation.x = isNewLocationInCircle ? boundedLocation.x : (isCurrentLocationInCircle ? table.ballsP[i].x : snookerCircleCenter.x);
                    boundedLocation.z = isNewLocationInCircle ? boundedLocation.z : (isCurrentLocationInCircle ? table.ballsP[i].z : snookerCircleCenter.z);
                }
                // no collisions, we can update the position and reset the pickup
                table.ballsP[i] = boundedLocation;

                pickupTransform.localPosition = Vector3.zero;
                pickupTransform.localRotation = Quaternion.identity;
            }
        }
    }
    private bool IsInSemiCircle(Vector3 location, Vector3 semiCircleCenter, float radius)
    {
        float distance = Vector3.Distance(location, semiCircleCenter);

        if (distance < radius && location.x < semiCircleCenter.x)
            return true;
        return false;
    }
    public void _BeginReposition(Repositioner grip)
    {
        if (!canReposition(grip))
        {
            grip._Drop();
            return;
        }

        int idx = grip.idx;
        if (repositioning[idx]) return;
        
        repositioning[idx] = true;
        repositionCount++;
        return;
    }

    public void _EndReposition(Repositioner grip)
    {
        int idx = grip.idx;
        if (!repositioning[idx]) return;
        
        repositioning[idx] = false;
        repositionCount--;

        grip._Reset();
        
        table._TriggerPlaceBall(idx);
    }

    private bool canReposition(Repositioner grip)
    {
        VRCPlayerApi self = Networking.LocalPlayer;
        if (!table.gameLive)
        {
            return false;
        }
        if (!table._IsPlayer(self) && !table._IsReferee(self))
        {
            return false;
        }
        if (grip.idx > 0)
        {
            if (!table.isPracticeMode && !table._IsReferee(self))
            {
                return false;
            }
        }

        return true;
    }
}
