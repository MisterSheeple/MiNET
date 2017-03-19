﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using log4net;
using MiNET.Blocks;
using MiNET.Entities.Hostile;
using MiNET.Entities.Passive;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Entities
{
	public class EntitySpawnManager
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (EntitySpawnManager));

		public const int CapHostile = 70;
		public const int CapPassive = 30; // 10
		public const int CapAmbient = 15;
		public const int CapWater = 5;

		public static readonly EntityType[] PassiveMobs = {EntityType.Chicken, EntityType.Cow, EntityType.Pig, EntityType.Sheep, EntityType.Wolf, EntityType.Horse};

		public Level Level { get; set; }

		public EntitySpawnManager(Level level)
		{
			Level = level;
		}

		public virtual void AttemptHostileMobSpawn(long tickTime, BlockCoordinates blockCoordinates)
		{
		}

		public virtual void AttemptPassiveMobSpawn(long tickTime, BlockCoordinates blockCoordinates, int numberOfLoadedChunks)
		{
			if (tickTime%400 != 0) return;

			foreach (var entity in Level.GetEntites())
			{
				if (Level.GetSpawnedPlayers().Count(e => Vector3.Distance(entity.KnownPosition, e.KnownPosition) < 128) == 0)
				{
					Log.Warn($"Despawned entity because no players within 128 blocks distance");

					entity.DespawnEntity();
					return;
				}
			}

			var effectiveChunkCount = Math.Max(17*17, numberOfLoadedChunks);
			int entityCount = Level.GetEntites().Count(entity => Vector3.Distance(blockCoordinates, entity.KnownPosition) < effectiveChunkCount);
			if (entityCount > CapPassive*effectiveChunkCount/289)
			{
				//Log.Warn($"Reached mob cap at {entityCount}, skipping");
				return;
			}

			var random = Level.Random;

			int maxPackSize = 0;
			int numberOfSpawnedMobs = 0;

			EntityType entityType = EntityType.None;

			for (int i = 0; i < 12; i++)
			{
				int x = random.Next(20) + blockCoordinates.X;
				int y = blockCoordinates.Y;
				int z = random.Next(20) + blockCoordinates.Z;

				var spawnBlock = Level.GetBlock(x, y, z);
				if (spawnBlock is Grass || spawnBlock is Sand || spawnBlock is Gravel)
				{
					if (entityType == EntityType.None)
					{
						entityType = SelectEntityType(spawnBlock);
						maxPackSize = entityType == EntityType.Wolf ? 8 : 4;
						if (entityType == EntityType.None) return;
					}

					var firstBlock = Level.GetBlock(x, y + 1, z);
					if (!firstBlock.IsSolid && (firstBlock.BlockLight >= 9 || firstBlock.SkyLight >= 9))
					{
						var secondBlock = Level.GetBlock(x, y + 2, z);
						if (!secondBlock.IsSolid)
						{
							var yaw = random.Next(360);

							if (SpawnPassive(new PlayerLocation(x, y + 1, z, yaw + 15, yaw), entityType))
							{
								if (++numberOfSpawnedMobs >= maxPackSize) return;
							}
							else
							{
								Log.Warn($"Failed to spawn {entityType} because area not clear");
							}
						}
					}
				}
			}
		}

		private EntityType SelectEntityType(Block spawnBlock)
		{
			// Only choose from the ones that fits the location. Need to implement that filtering.
			// For now, just use all general friendly/passive mobs.

			List<EntityType> possibleMobs = new List<EntityType>(new[]
			{
				EntityType.Chicken, EntityType.Cow, EntityType.Pig, EntityType.Sheep
			});

			if (new byte[] {5, 19, 30, 31, 32, 33, 133, 158, 160, 161}.Contains(spawnBlock.BiomeId))
			{
				possibleMobs.Add(EntityType.Wolf);
			}

			if (new byte[] {1, 35, 36, 163, 164}.Contains(spawnBlock.BiomeId))
			{
				possibleMobs.Add(EntityType.Horse);
			}

			if (new byte[] {21, 22, 23, 149, 151}.Contains(spawnBlock.BiomeId))
			{
				possibleMobs.Add(EntityType.Ocelot);
			}

			if (spawnBlock.BiomeId == 2 || BiomeUtils.Biomes.Where(biome => biome.Temperature < 0).Count(biome => biome.Id == spawnBlock.BiomeId) == 1)
			{
				// This rule is wrong. Should exempt cold taigas
				possibleMobs.Clear();
				possibleMobs.Add(EntityType.Rabbit);
			}

			if (new byte[] {132}.Contains(spawnBlock.BiomeId))
			{
				possibleMobs.Add(EntityType.Rabbit);
			}

			if (new byte[] {7, 16, 25, 0, 37, 38, 39, 165, 166, 167}.Contains(spawnBlock.BiomeId))
			{
				possibleMobs.Clear();
				possibleMobs.Add(EntityType.None);
			}

			EntityType entityType = possibleMobs[Level.Random.Next(possibleMobs.Count)];

			return entityType;
		}

		private bool SpawnPassive(PlayerLocation position, EntityType entityType)
		{
			Level world = Level;
			Mob mob = null;
			switch (entityType)
			{
				case EntityType.Chicken:
					mob = new Chicken(world);
					break;
				case EntityType.Cow:
					mob = new Cow(world);
					break;
				case EntityType.Pig:
					mob = new Pig(world);
					break;
				case EntityType.Sheep:
					mob = new Sheep(world);
					break;
				case EntityType.Wolf:
					mob = new Wolf(world);
					break;
				case EntityType.Horse:
					mob = new Horse(world);
					break;
				case EntityType.Ocelot:
					mob = new Ocelot(world);
					break;
				case EntityType.Rabbit:
					mob = new Rabbit(world);
					break;
			}

			if (mob == null) return false;

			mob.KnownPosition = position;
			var bbox = mob.GetBoundingBox();
			if (!SpawnAreaClear(bbox))
			{
				return false;
			}


			mob.SpawnEntity();


			Log.Warn($"Spawn friendly {entityType}");
			return true;
		}

		private bool SpawnAreaClear(BoundingBox bbox)
		{
			for (int x = (int) bbox.Min.X; x < bbox.Max.X; x++)
			{
				for (int y = (int) bbox.Min.Y; y < bbox.Max.Y; y++)
				{
					for (int z = (int) bbox.Min.Z; z < bbox.Max.Z; z++)
					{
						if (!Level.IsAir(new BlockCoordinates(x, y, z))) return false;
					}
				}
			}

			return true;
		}
	}
}