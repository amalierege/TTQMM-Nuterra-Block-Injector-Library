﻿using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

public class ModuleDampener : Module
{
    public bool ApplyAtPosition = false;
    public float Strength = 10f;

    void FixedUpdate()
    {
        if (block.IsAttached && block.tank != null && !block.tank.beam.IsActive)
        {
            var rigidbody = block.tank.rbody;
            Vector3 val = rigidbody.velocity * -Mathf.Min(Strength, rigidbody.mass);
            if (ApplyAtPosition)
                rigidbody.AddForceAtPosition(val, block.CentreOfMass);
            else
                rigidbody.AddForce(val);
        }
    }
}

public class SetfuseTimer : MonoBehaviour
{
    void Spawn()
    {

    }
}

public class ModuleRecipeWrapper : MonoBehaviour
{
    [SerializeField]
    private RecipeListWrapper _RecipeList = null;
    private bool _alreadySet = false;

    public string RecipeName
    {
        set
        {
            if (_alreadySet) throw new Exception("ModuleRecipeWrapper has already been set, cannot define a new value!");
            _alreadySet = true;
            m_RecipeListNames.SetValue(this, new RecipeManager.RecipeNameWrapper[] { new RecipeManager.RecipeNameWrapper { inverted = false, name = value } });
        }
    }


    public List<RecipeTable.Recipe> Recipes
    {
        set
        {
            if (_alreadySet) throw new Exception("ModuleRecipeWrapper has already been set, cannot define a new value!");
            var _recipeList = new RecipeTable.RecipeList()
            {
                m_Name = gameObject.name,
                m_Recipes = value
            };
            RecipeListWrapper recipeListWrapper = ScriptableObject.CreateInstance<RecipeListWrapper>();
            recipeListWrapper.name = _recipeList.m_Name;
            recipeListWrapper.target = _recipeList;
            m_RecipeListNames.SetValue(this, Array.Empty<RecipeManager.RecipeNameWrapper>());
        }
    }

    /// <summary>
    /// <see cref="RecipeListWrapper"/>[]
    /// </summary>
    private static readonly FieldInfo m_RecipeLists = typeof(ModuleRecipeProvider).GetField("m_RecipeLists", BindingFlags.Instance | BindingFlags.NonPublic);
    /// <summary>
    /// <see cref="RecipeManager.RecipeNameWrapper"/>[]
    /// </summary>
    private static readonly FieldInfo m_RecipeListNames = typeof(ModuleRecipeProvider).GetField("m_RecipeListNames", BindingFlags.Instance | BindingFlags.NonPublic);

    void PrePool()
    { m_RecipeLists.SetValue(this, new RecipeListWrapper[] { _RecipeList }); }
}

public class ModuleStopSpinnersOnDamage : Module
{
    public Spinner[] targetSpinners = null;
    public bool SetFullSpeedInstead = false;
    void OnPool()
    {
        block.visible.damageable.damageEvent.Subscribe(OnDamage);
        if (targetSpinners == null || targetSpinners.Length == 0)
        {
            targetSpinners = GetComponentsInChildren<Spinner>(true);
        }
    }

    void OnDamage(ManDamage.DamageInfo info)
    {
        foreach(Spinner target in targetSpinners)
        {
            Vector3 axis = target.m_RotationAxis, perp = new Vector3(0f, 1f - axis.y, axis.y);
            float angle = Vector3.SignedAngle(perp, target.trans.localRotation * perp, axis);
            target.SetAutoSpin(SetFullSpeedInstead);
            target.Reset();
            target.SetAngle(angle);
        }
    }
}

public class ModuleSpinWhenAnchored : Module
{
    public Spinner[] targetSpinners = null;
    public bool Invert = false;
    public bool OnWhileDetached = false;

    void OnPool()
    {
        block.AttachEvent.Subscribe(OnAttach);
        block.DetachEvent.Subscribe(OnDetach);
        if (targetSpinners == null || targetSpinners.Length == 0)
            targetSpinners = GetComponentsInChildren<Spinner>(true);
    }

    private void OnSpawn()
    {
        SetSpinners(OnWhileDetached);
    }

    private void OnAttach()
    {
        block.tank.AnchorEvent.Subscribe(OnAnchor);
        SetSpinners(block.tank.IsAnchored != Invert);
    }

    private void OnDetach() 
    { 
        if (block.tank != null) block.tank.AnchorEvent.Unsubscribe(OnAnchor);
        SetSpinners(OnWhileDetached);
    }      

    private void OnAnchor(ModuleAnchor _, bool IsAnchored, bool __)
    {
        SetSpinners(IsAnchored != Invert);
    }

    private void SetSpinners(bool State)
    {
        foreach (Spinner target in targetSpinners)
            target.SetAutoSpin(State);
    }
}

[RequireComponent(typeof(ModuleEnergyStore))]
[RequireComponent(typeof(ModuleEnergy))]
public class ModuleHealOverTime : Module
{
    public ModuleEnergyStore _EnergyStore;
    public ModuleEnergy _Energy;
    public float _timeout;

    public float EnergyDrain = 10f;
    public float HealAmount = 10;
    public float HealDelay = 1.0f;
    public float Capacity = 100f;
    public float EnergyMinLimit = 50f;

    public float ActualCurrentEnergy
    {
        get
        {
            var e = block.tank.EnergyRegulator.Energy(EnergyRegulator.EnergyType.Electric);
            return e.storageTotal - e.spareCapacity;
        }
    }

    void Update()
    {
        if (_timeout > 0)
        {
            _timeout -= Time.deltaTime;
        }
        else if (block.tank != null)
        {
            var damageable = block.visible.damageable;
            if (damageable.Health < damageable.MaxHealth - HealAmount)
                OnFire(damageable);
        }
    }

    public void PrePool()
    {
        _EnergyStore = GetComponent<ModuleEnergyStore>();
        _Energy = GetComponent<ModuleEnergy>();
        _EnergyStore.m_Capacity = Capacity;
    }

    public void OnPool()
    {
        block.AttachEvent.Subscribe(OnAttach);
    }

    void OnSpawn()
    {
        _timeout = HealDelay;
    }

    void OnAttach()
    {
        _timeout = HealDelay;
    }

    void OnFire(Damageable damageable)
    {
        _timeout = HealDelay;
        if (ActualCurrentEnergy < EnergyDrain + EnergyMinLimit) return;
        _EnergyStore.AddEnergy(-EnergyDrain);
        damageable.Repair(HealAmount);
        block.visible.KeepAwake();
    }
}

public class ProjectileDamageOverTime : MonoBehaviour
{
    public float DamageOverTime = 50f;
    public int MaxHits = 8;
    public ManDamage.DamageType DamageType = ManDamage.DamageType.Standard;
    public bool FriendlyFire = false;
    public float TeamMultiplier = 1f;
    public float SceneryMultiplier = 1f;
    public float DetachedMultiplier = 1f;
    public bool DamageTouch = true;
    public bool DamageStuck = true;
    int _CurrentHits;
    Damageable[] _Hits;
    Projectile _Projectile;
    Damageable _stuckOn;

    private void OnCollisionStay(Collision collision)
    {
        if (!enabled || !DamageTouch || _CurrentHits > MaxHits) return;

        ContactPoint[] contacts = collision.contacts;
        if (contacts.Length == 0)
            return;

        ContactPoint contactPoint = contacts[0];
        Damageable v = contactPoint.otherCollider.GetComponentInParent<Damageable>();
        if (v == null) v = contactPoint.thisCollider.GetComponentInParent<Damageable>();
        if (v == null) return;

        if (!FriendlyFire)
        {
            TankBlock block = v.Block;
            if (block != null && block.LastTechTeam == _Projectile.Shooter.Team)
                return;
        }
        _Hits[_CurrentHits] = v;
        _CurrentHits++;
    }

    private void OnTriggerStay(Collider other)
    {
        if (!enabled || !DamageTouch || _CurrentHits > MaxHits) return;

        Damageable v = other.GetComponentInParent<Damageable>();
        if (v == null) return;

        if (!FriendlyFire)
        {
            TankBlock block = v.Block;
            if (block != null && block.LastTechTeam == _Projectile.Shooter.Team)
                return;
        }
        _Hits[_CurrentHits] = v;
        _CurrentHits++;
    }

    private void FixedUpdate()
    {
        if (!enabled)
        {
            _CurrentHits = 0;
            return;
        }
        if (_CurrentHits != 0) 
        {

            float damage = DamageOverTime * Time.fixedDeltaTime / _CurrentHits;
            for (int i = 0; i < _CurrentHits; i++)
            {
                var hit = _Hits[i];
                if (hit != null)
                {
                    float thisDamage = damage;
                    TankBlock block = hit.Block;
                    if (block == null)
                        thisDamage *= SceneryMultiplier;
                    else if (block.tank == null)
                        thisDamage *= DetachedMultiplier;
                    else if (block.LastTechTeam == _Projectile.Shooter.Team)
                        thisDamage *= TeamMultiplier;
                    if (thisDamage < 0f)
                        hit.Repair(thisDamage, true);
                    else if (thisDamage > 0f)
                        ManDamage.inst.DealDamage(hit, thisDamage, DamageType, this);
                }
            }
        }
        if (DamageStuck)
        {
            if (_Projectile.Stuck && transform.parent != null)
            {
                if (_stuckOn == null)
                    _stuckOn = transform.parent.GetComponentInParent<Damageable>();
                if (_stuckOn != null)
                {
                    _Hits[0] = _stuckOn;
                    _CurrentHits = 1;
                    return;
                }
            }
            else if (_stuckOn != null)
                _stuckOn = null;
        }
        _CurrentHits = 0;
    }

    void OnPool()
    {
        _Hits = new Damageable[MaxHits];
        _Projectile = GetComponent<Projectile>();
        var rbody = GetComponent<Rigidbody>();
        if (rbody != null) rbody.detectCollisions = true;
    }
}

public class ModuleFloater : MotionBlocks.ModuleFloater { }

// ... 

namespace MotionBlocks
{
    public class ModuleFloater : Module
    {
        public float MinHeight = -85f;
        public float MaxHeight = 400f;
        public float MaxStrength = 14f;
        public float VelocityDampen = 0.08f;
        void FixedUpdate()
        {
            if (block.IsAttached && block.tank != null && !block.tank.beam.IsActive)
            {
                Vector3 blockCenter = block.centreOfMassWorld;
                float blockForce = (MaxStrength / MaxHeight) * (MaxHeight - blockCenter.y)
                      - block.tank.rbody.GetPointVelocity(blockCenter).y * VelocityDampen;
                if (MaxStrength > 0)
                    block.tank.rbody.AddForceAtPosition(Vector3.up * Mathf.Clamp(blockForce, 0f, MaxStrength * 1.25f),
                        blockCenter, ForceMode.Impulse);
                else
                    block.tank.rbody.AddForceAtPosition(Vector3.up * Mathf.Clamp(blockForce, MaxStrength * 1.25f, 0f),
                        blockCenter, ForceMode.Impulse);
            }
        }
    }
}
