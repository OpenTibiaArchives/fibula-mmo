﻿// -----------------------------------------------------------------
// <copyright file="OperationContext.cs" company="2Dudes">
// Copyright (c) | Jose L. Nunez de Caceres et al.
// https://linkedin.com/in/nunezdecaceres
//
// All Rights Reserved.
//
// Licensed under the MIT License. See LICENSE in the project root for license information.
// </copyright>
// -----------------------------------------------------------------

namespace Fibula.Mechanics.Operations
{
    using Fibula.Common.Utilities;
    using Fibula.Creatures.Contracts.Abstractions;
    using Fibula.Items.Contracts.Abstractions;
    using Fibula.Map.Contracts.Abstractions;
    using Fibula.Mechanics.Contracts.Abstractions;
    using Fibula.Scheduling;
    using Fibula.Scheduling.Contracts.Abstractions;
    using Serilog;

    /// <summary>
    /// Class that represents a context for operations.
    /// </summary>
    public class OperationContext : EventContext, IOperationContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OperationContext"/> class.
        /// </summary>
        /// <param name="logger">A reference to the logger in use.</param>
        /// <param name="mapDescriptor">A reference to the map descriptor in use.</param>
        /// <param name="map">A reference to the map in use.</param>
        /// <param name="creatureFinder">A reference to the creature finder in use.</param>
        /// <param name="itemFactory">A reference to the item factory in use.</param>
        /// <param name="creatureFactory">A reference to the creature factory in use.</param>
        /// <param name="containerManager">A reference to the container manager in use.</param>
        /// <param name="gameOperationsApi">A reference to the game operations api.</param>
        /// <param name="combatOperationsApi">A reference to the combat operations api.</param>
        /// <param name="pathFinderAlgo">A reference to the path finding algorithm in use.</param>
        /// <param name="predefinedItemSet">A reference to the predefined item set declared.</param>
        /// <param name="scheduler">A reference to the scheduler instance.</param>
        public OperationContext(
            ILogger logger,
            IMapDescriptor mapDescriptor,
            IMap map,
            ICreatureFinder creatureFinder,
            IItemFactory itemFactory,
            ICreatureFactory creatureFactory,
            IContainerManager containerManager,
            IGameOperationsApi gameOperationsApi,
            ICombatOperationsApi combatOperationsApi,
            IPathFinder pathFinderAlgo,
            IPredefinedItemSet predefinedItemSet,
            IScheduler scheduler)
            : base(logger)
        {
            mapDescriptor.ThrowIfNull(nameof(mapDescriptor));
            map.ThrowIfNull(nameof(map));
            creatureFinder.ThrowIfNull(nameof(creatureFinder));
            itemFactory.ThrowIfNull(nameof(itemFactory));
            creatureFactory.ThrowIfNull(nameof(creatureFactory));
            containerManager.ThrowIfNull(nameof(containerManager));
            gameOperationsApi.ThrowIfNull(nameof(gameOperationsApi));
            combatOperationsApi.ThrowIfNull(nameof(combatOperationsApi));
            pathFinderAlgo.ThrowIfNull(nameof(pathFinderAlgo));
            predefinedItemSet.ThrowIfNull(nameof(predefinedItemSet));
            scheduler.ThrowIfNull(nameof(scheduler));

            this.MapDescriptor = mapDescriptor;
            this.Map = map;
            this.CreatureFinder = creatureFinder;
            this.ItemFactory = itemFactory;
            this.CreatureFactory = creatureFactory;
            this.ContainerManager = containerManager;
            this.GameApi = gameOperationsApi;
            this.CombatApi = combatOperationsApi;
            this.PathFinder = pathFinderAlgo;
            this.PredefinedItemSet = predefinedItemSet;
            this.Scheduler = scheduler;
        }

        /// <summary>
        /// Gets a reference to the map descriptor in use.
        /// </summary>
        public IMapDescriptor MapDescriptor { get; }

        /// <summary>
        /// Gets the reference to the map.
        /// </summary>
        public IMap Map { get; }

        /// <summary>
        /// Gets the reference to the creature finder in use.
        /// </summary>
        public ICreatureFinder CreatureFinder { get; }

        /// <summary>
        /// Gets a reference to the item factory in use.
        /// </summary>
        public IItemFactory ItemFactory { get; }

        /// <summary>
        /// Gets a reference to the creature factory in use.
        /// </summary>
        public ICreatureFactory CreatureFactory { get; }

        /// <summary>
        /// Gets a reference to the container manager in use.
        /// </summary>
        public IContainerManager ContainerManager { get; }

        /// <summary>
        /// Gets a reference to the game's api.
        /// </summary>
        public IGameOperationsApi GameApi { get; }

        /// <summary>
        /// Gets a reference to the combat api.
        /// </summary>
        public ICombatOperationsApi CombatApi { get; }

        /// <summary>
        /// Gets a reference to the pathfinder algorithm in use.
        /// </summary>
        public IPathFinder PathFinder { get; }

        /// <summary>
        /// Gets the predefined item set.
        /// </summary>
        public IPredefinedItemSet PredefinedItemSet { get; }

        /// <summary>
        /// Gets a reference to the scheduler in use.
        /// </summary>
        public IScheduler Scheduler { get; }
    }
}
