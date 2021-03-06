﻿// -----------------------------------------------------------------
// <copyright file="CombatantCreature.cs" company="2Dudes">
// Copyright (c) | Jose L. Nunez de Caceres et al.
// https://linkedin.com/in/nunezdecaceres
//
// All Rights Reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------

namespace Fibula.Creatures
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Fibula.Common.Contracts.Enumerations;
    using Fibula.Creatures.Contracts.Abstractions;
    using Fibula.Data.Entities.Contracts.Enumerations;
    using Fibula.Mechanics.Contracts.Abstractions;
    using Fibula.Mechanics.Contracts.Combat.Enumerations;
    using Fibula.Mechanics.Contracts.Constants;
    using Fibula.Mechanics.Contracts.Delegates;
    using Fibula.Mechanics.Contracts.Enumerations;
    using Fibula.Mechanics.Contracts.Structs;

    /// <summary>
    /// Class that represents all creatures in the game.
    /// </summary>
    public abstract class CombatantCreature : Creature, ICombatant
    {
        /// <summary>
        /// Lock object to semaphore interaction with the exhaustion dictionary.
        /// </summary>
        private readonly object exhaustionLock;

        /// <summary>
        /// Stores the map of combatants to the damage taken from them for the current combat session.
        /// </summary>
        private readonly ConcurrentDictionary<uint, uint> combatSessionDamageTakenMap;

        /// <summary>
        /// The base of how fast a combatant can earn an attack credit per combat round.
        /// </summary>
        private readonly decimal baseAttackSpeed;

        /// <summary>
        /// The base of how fast a combatant can earn a defense credit per combat round.
        /// </summary>
        private readonly decimal baseDefenseSpeed;

        /// <summary>
        /// Stores the set of combatants currently attacking this combatant for the current combat session.
        /// </summary>
        private readonly ISet<ICombatant> combatSessionAttackedBy;

        /// <summary>
        /// The buff for attack speed.
        /// </summary>
        private decimal attackSpeedBuff;

        /// <summary>
        /// The buff for defense speed.
        /// </summary>
        private decimal defenseSpeedBuff;

        /// <summary>
        /// Initializes a new instance of the <see cref="CombatantCreature"/> class.
        /// </summary>
        /// <param name="name">The name of this creature.</param>
        /// <param name="article">An article for the name of this creature.</param>
        /// <param name="maxHitpoints">The maximum hitpoints of the creature.</param>
        /// <param name="maxManapoints">The maximum manapoints of the creature.</param>
        /// <param name="corpse">The corpse of the creature.</param>
        /// <param name="hitpoints">The current hitpoints of the creature.</param>
        /// <param name="manapoints">The current manapoints of the creature.</param>
        /// <param name="baseAttackSpeed">
        /// Optional. The base attack speed for this creature.
        /// Bounded between [<see cref="CombatConstants.MinimumCombatSpeed"/>, <see cref="CombatConstants.MaximumCombatSpeed"/>] inclusive.
        /// Defaults to <see cref="CombatConstants.DefaultAttackSpeed"/>.
        /// </param>
        /// <param name="baseDefenseSpeed">
        /// Optional. The base defense speed for this creature.
        /// Bounded between [<see cref="CombatConstants.MinimumCombatSpeed"/>, <see cref="CombatConstants.MaximumCombatSpeed"/>] inclusive.
        /// Defaults to <see cref="CombatConstants.DefaultDefenseSpeed"/>.
        /// </param>
        protected CombatantCreature(
            string name,
            string article,
            ushort maxHitpoints,
            ushort maxManapoints,
            ushort corpse,
            ushort hitpoints = 0,
            ushort manapoints = 0,
            decimal baseAttackSpeed = CombatConstants.DefaultAttackSpeed,
            decimal baseDefenseSpeed = CombatConstants.DefaultDefenseSpeed)
            : base(name, article, maxHitpoints, maxManapoints, corpse, hitpoints, manapoints)
        {
            // Normalize combat speeds.
            this.baseAttackSpeed = Math.Min(CombatConstants.MaximumCombatSpeed, Math.Max(CombatConstants.MinimumCombatSpeed, baseAttackSpeed));
            this.baseDefenseSpeed = Math.Min(CombatConstants.MaximumCombatSpeed, Math.Max(CombatConstants.MinimumCombatSpeed, baseDefenseSpeed));

            this.AutoAttackCredits = this.AutoAttackMaximumCredits;
            this.AutoDefenseCredits = this.AutoDefenseMaximumCredits;

            this.exhaustionLock = new object();
            this.ExhaustionInformation = new Dictionary<ExhaustionType, DateTimeOffset>();

            this.combatSessionDamageTakenMap = new ConcurrentDictionary<uint, uint>();
            this.combatSessionAttackedBy = new HashSet<ICombatant>();

            this.Skills = new Dictionary<SkillType, ISkill>();
        }

        /// <summary>
        /// Event to call when the combatant's health changes.
        /// </summary>
        public event OnHealthChanged HealthChanged;

        /// <summary>
        /// Event to call when the combatant dies.
        /// </summary>
        public event OnDeath Death;

        /// <summary>
        /// Event to call when the attack target changes.
        /// </summary>
        public event OnAttackTargetChanged AttackTargetChanged;

        /// <summary>
        /// Event to call when the follow target changes.
        /// </summary>
        public event OnFollowTargetChanged FollowTargetChanged;

        /// <summary>
        /// Event triggered when this a skill of this creature changes.
        /// </summary>
        public event OnCreatureSkillChanged SkillChanged;

        /// <summary>
        /// Gets or sets the target being chased, if any.
        /// </summary>
        public ICreature ChaseTarget { get; protected set; }

        /// <summary>
        /// Gets the current target combatant.
        /// </summary>
        public ICombatant AutoAttackTarget { get; private set; }

        /// <summary>
        /// Gets the number of attack credits available.
        /// </summary>
        public int AutoAttackCredits { get; private set; }

        /// <summary>
        /// Gets the number of maximum attack credits.
        /// </summary>
        public ushort AutoAttackMaximumCredits => CombatConstants.DefaultMaximumAttackCredits;

        /// <summary>
        /// Gets the number of auto defense credits available.
        /// </summary>
        public int AutoDefenseCredits { get; private set; }

        /// <summary>
        /// Gets the number of maximum defense credits.
        /// </summary>
        public ushort AutoDefenseMaximumCredits => CombatConstants.DefaultMaximumDefenseCredits;

        /// <summary>
        /// Gets a metric of how fast a combatant can earn an attack credit per combat round.
        /// </summary>
        public decimal AttackSpeed => this.baseAttackSpeed + this.attackSpeedBuff;

        /// <summary>
        /// Gets a metric of how fast a combatant can earn a defense credit per combat round.
        /// </summary>
        public decimal DefenseSpeed => this.baseDefenseSpeed + this.defenseSpeedBuff;

        /// <summary>
        /// Gets or sets the fight mode selected by this combatant.
        /// </summary>
        public FightMode FightMode { get; set; }

        /// <summary>
        /// Gets or sets the chase mode selected by this combatant.
        /// </summary>
        public ChaseMode ChaseMode { get; set; }

        /// <summary>
        /// Gets the range that the auto attack has.
        /// </summary>
        public abstract byte AutoAttackRange { get; }

        /// <summary>
        /// Gets the distribution of damage taken by any combatant that has attacked this combatant while the current combat is active.
        /// </summary>
        public IEnumerable<(uint, uint)> DamageTakenInSession
        {
            get
            {
                return this.combatSessionDamageTakenMap.Select(kvp => (kvp.Key, kvp.Value)).ToList();
            }
        }

        /// <summary>
        /// Gets the collection of combatants currently attacking this combatant.
        /// </summary>
        public IEnumerable<ICombatant> AttackedBy
        {
            get
            {
                return this.combatSessionAttackedBy.ToList();
            }
        }

        /// <summary>
        /// Gets the current exhaustion information for the entity.
        /// </summary>
        /// <remarks>
        /// The key is a <see cref="ExhaustionType"/>, and the value is a <see cref="DateTimeOffset"/>: the date and time
        /// at which exhaustion is completely recovered.
        /// </remarks>
        public IDictionary<ExhaustionType, DateTimeOffset> ExhaustionInformation { get; }

        /// <summary>
        /// Gets the current skills information for the combatant.
        /// </summary>
        /// <remarks>
        /// The key is a <see cref="SkillType"/>, and the value is a <see cref="ISkill"/>.
        /// </remarks>
        public IDictionary<SkillType, ISkill> Skills { get; }

        /// <summary>
        /// Consumes combat credits to the combatant.
        /// </summary>
        /// <param name="creditType">The type of combat credits to consume.</param>
        /// <param name="amount">The amount of credits to consume.</param>
        public void ConsumeCredits(CombatCreditType creditType, byte amount)
        {
            switch (creditType)
            {
                case CombatCreditType.Attack:
                    this.AutoAttackCredits -= amount;
                    break;

                case CombatCreditType.Defense:
                    this.AutoDefenseCredits -= amount;
                    break;
            }
        }

        /// <summary>
        /// Restores combat credits to the combatant.
        /// </summary>
        /// <param name="creditType">The type of combat credits to restore.</param>
        /// <param name="amount">The amount of credits to restore.</param>
        public void RestoreCredits(CombatCreditType creditType, byte amount)
        {
            switch (creditType)
            {
                case CombatCreditType.Attack:
                    this.AutoAttackCredits = Math.Min(this.AutoAttackMaximumCredits, this.AutoAttackCredits + amount);
                    break;

                case CombatCreditType.Defense:
                    this.AutoDefenseCredits = Math.Min(this.AutoDefenseMaximumCredits, this.AutoDefenseCredits + amount);
                    break;
            }
        }

        /// <summary>
        /// Sets the attack target of this combatant.
        /// </summary>
        /// <param name="otherCombatant">The other target combatant, if any.</param>
        /// <returns>True if the target was actually changed, false otherwise.</returns>
        public bool SetAttackTarget(ICombatant otherCombatant)
        {
            bool targetWasChanged = false;

            if (otherCombatant != this.AutoAttackTarget)
            {
                var oldTarget = this.AutoAttackTarget;

                this.AutoAttackTarget = otherCombatant;

                oldTarget?.UnsetAttackedBy(this);
                otherCombatant?.SetAttackedBy(this);

                if (this.ChaseMode != ChaseMode.Stand)
                {
                    this.SetFollowTarget(otherCombatant);
                }

                this.AttackTargetChanged?.Invoke(this, oldTarget);

                targetWasChanged = true;
            }

            return targetWasChanged;
        }

        /// <summary>
        /// Sets the chasing target of this combatant.
        /// </summary>
        /// <param name="target">The target to chase, if any.</param>
        /// <returns>True if the target was actually changed, false otherwise.</returns>
        public bool SetFollowTarget(ICreature target)
        {
            bool targetWasChanged = false;

            if (target != this.ChaseTarget)
            {
                var oldTarget = this.ChaseTarget;

                this.ChaseTarget = target;

                this.FollowTargetChanged?.Invoke(this, oldTarget);

                targetWasChanged = true;
            }

            return targetWasChanged;
        }

        /// <summary>
        /// Calculates the remaining <see cref="TimeSpan"/> until the entity's exhaustion is recovered from.
        /// </summary>
        /// <param name="type">The type of exhaustion.</param>
        /// <param name="currentTime">The current time to calculate from.</param>
        /// <returns>The <see cref="TimeSpan"/> result.</returns>
        public TimeSpan CalculateRemainingCooldownTime(ExhaustionType type, DateTimeOffset currentTime)
        {
            lock (this.exhaustionLock)
            {
                if (!this.ExhaustionInformation.TryGetValue(type, out DateTimeOffset readyAtTime))
                {
                    return TimeSpan.Zero;
                }

                var timeLeft = readyAtTime - currentTime;

                if (timeLeft < TimeSpan.Zero)
                {
                    this.ExhaustionInformation.Remove(type);

                    return TimeSpan.Zero;
                }

                return timeLeft;
            }
        }

        /// <summary>
        /// Adds exhaustion of the given type.
        /// </summary>
        /// <param name="type">The type of exhaustion to add.</param>
        /// <param name="fromTime">The reference time from which to add.</param>
        /// <param name="timeSpan">The amount of time to add exhaustion for.</param>
        public void AddExhaustion(ExhaustionType type, DateTimeOffset fromTime, TimeSpan timeSpan)
        {
            lock (this.exhaustionLock)
            {
                if (this.ExhaustionInformation.ContainsKey(type) && this.ExhaustionInformation[type] > fromTime)
                {
                    fromTime = this.ExhaustionInformation[type];
                }

                this.ExhaustionInformation[type] = fromTime + timeSpan;
            }
        }

        /// <summary>
        /// Adds exhaustion of the given type.
        /// </summary>
        /// <param name="type">The type of exhaustion to add.</param>
        /// <param name="fromTime">The reference time from which to add.</param>
        /// <param name="milliseconds">The amount of time in milliseconds to add exhaustion for.</param>
        public void AddExhaustion(ExhaustionType type, DateTimeOffset fromTime, uint milliseconds)
        {
            this.AddExhaustion(type, fromTime, TimeSpan.FromMilliseconds(milliseconds));
        }

        /// <summary>
        /// Calculates the current percentual value between current and target counts for the given skill.
        /// </summary>
        /// <param name="skillType">The type of skill to calculate for.</param>
        /// <returns>A value between [0, 100] representing the current percentual value.</returns>
        public byte CalculateSkillPercent(SkillType skillType)
        {
            const int LowerBound = 0;
            const int UpperBound = 100;

            if (!this.Skills.ContainsKey(skillType))
            {
                return LowerBound;
            }

            var unadjustedPercent = Math.Max(LowerBound, Math.Min(this.Skills[skillType].Count / this.Skills[skillType].TargetCount, UpperBound));

            return (byte)Math.Floor(unadjustedPercent);
        }

        /// <summary>
        /// Starts tracking another <see cref="ICombatant"/>.
        /// </summary>
        /// <param name="otherCombatant">The other combatant, now in view.</param>
        public abstract void AddToCombatList(ICombatant otherCombatant);

        /// <summary>
        /// Stops tracking another <see cref="ICombatant"/>.
        /// </summary>
        /// <param name="otherCombatant">The other combatant, now in view.</param>
        public abstract void RemoveFromCombatList(ICombatant otherCombatant);

        /// <summary>
        /// Sets this combatant as being attacked by another.
        /// </summary>
        /// <param name="combatant">The combatant attacking this one, if any.</param>
        public void SetAttackedBy(ICombatant combatant)
        {
            if (combatant == null)
            {
                return;
            }

            this.combatSessionAttackedBy.Add(combatant);
        }

        /// <summary>
        /// Unsets this combatant as being attacked by another.
        /// </summary>
        /// <param name="combatant">The combatant no longer attacking this one, if any.</param>
        public void UnsetAttackedBy(ICombatant combatant)
        {
            if (combatant == null)
            {
                return;
            }

            this.combatSessionAttackedBy.Remove(combatant);
        }

        /// <summary>
        /// Applies damage to the combatant, which is expected to apply reductions and protections.
        /// </summary>
        /// <param name="damageInfo">The information of the damage to make, without reductions.</param>
        /// <param name="fromCombatantId">The combatant from which to track the damage, if any.</param>
        /// <returns>The information about the damage actually done.</returns>
        public DamageInfo ApplyDamage(DamageInfo damageInfo, uint fromCombatantId = 0)
        {
            var oldHitpointsValue = this.Hitpoints;

            this.ApplyDamageModifiers(ref damageInfo);

            if (damageInfo.Damage < 0)
            {
                // heal instead.
                damageInfo.Damage = Math.Max(damageInfo.Damage, this.Hitpoints - this.MaxHitpoints);

                this.Hitpoints = (ushort)(this.Hitpoints - damageInfo.Damage);
            }
            else if (damageInfo.Damage > 0)
            {
                damageInfo.Damage = Math.Min(damageInfo.Damage, this.Hitpoints);
                damageInfo.Blood = this.BloodType;
                damageInfo.Effect = this.BloodType switch
                {
                    BloodType.Bones => AnimatedEffect.XGray,
                    BloodType.Fire => AnimatedEffect.XBlood,
                    BloodType.Slime => AnimatedEffect.Poison,
                    _ => AnimatedEffect.XBlood,
                };

                this.Hitpoints = (ushort)(this.Hitpoints - damageInfo.Damage);
            }

            if (this.Hitpoints != oldHitpointsValue)
            {
                this.HealthChanged?.Invoke(this, oldHitpointsValue);
            }

            if (fromCombatantId > 0)
            {
                this.combatSessionDamageTakenMap.AddOrUpdate(fromCombatantId, (uint)damageInfo.Damage, (key, oldValue) => (uint)(oldValue + damageInfo.Damage));
            }

            if (this.Hitpoints == 0)
            {
                this.Death?.Invoke(this);
            }

            return damageInfo;
        }

        /// <summary>
        /// Increases the attack speed of this combatant.
        /// </summary>
        /// <param name="increaseAmount">The amount by which to increase.</param>
        // TODO: this is just for testing purposes and should be removed.
        public void IncreaseAttackSpeed(decimal increaseAmount)
        {
            this.attackSpeedBuff = Math.Min(CombatConstants.MaximumCombatSpeed - this.baseAttackSpeed, this.attackSpeedBuff + increaseAmount);
        }

        /// <summary>
        /// Decreases the attack speed of this combatant.
        /// </summary>
        /// <param name="decreaseAmount">The amount by which to decrease.</param>
        // TODO: this is just for testing purposes and should be removed.
        public void DecreaseAttackSpeed(decimal decreaseAmount)
        {
            this.attackSpeedBuff = Math.Max(0, this.attackSpeedBuff - decreaseAmount);
        }

        /// <summary>
        /// Increases the defense speed of this combatant.
        /// </summary>
        /// <param name="increaseAmount">The amount by which to increase.</param>
        // TODO: this is just for testing purposes and should be removed.
        public void IncreaseDefenseSpeed(decimal increaseAmount)
        {
            this.defenseSpeedBuff = Math.Min(CombatConstants.MaximumCombatSpeed - this.baseDefenseSpeed, this.defenseSpeedBuff + increaseAmount);
        }

        /// <summary>
        /// Decreases the defense speed of this combatant.
        /// </summary>
        /// <param name="decreaseAmount">The amount by which to decrease.</param>
        // TODO: this is just for testing purposes and should be removed.
        public void DecreaseDefenseSpeed(decimal decreaseAmount)
        {
            this.defenseSpeedBuff = Math.Max(0, this.defenseSpeedBuff - decreaseAmount);
        }

        /// <summary>
        /// Raises the <see cref="SkillChanged"/> event for this creature on the given skill.
        /// </summary>
        /// <param name="forSkill">The skill to advance.</param>
        /// <param name="previousLevel">The previous skill level.</param>
        /// <param name="previousPercent">The previous percent of completion to next level.</param>
        protected void RaiseSkillChange(SkillType forSkill, uint previousLevel, byte previousPercent)
        {
            if (!this.Skills.ContainsKey(forSkill))
            {
                return;
            }

            this.SkillChanged?.Invoke(this, this.Skills[forSkill], previousLevel, previousPercent);
        }

        /// <summary>
        /// Applies damage modifiers to the damage information provided.
        /// </summary>
        /// <param name="damageInfo">The damage information.</param>
        protected abstract void ApplyDamageModifiers(ref DamageInfo damageInfo);
    }
}
