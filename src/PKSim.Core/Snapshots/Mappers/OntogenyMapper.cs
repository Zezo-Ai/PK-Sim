﻿using System.Threading.Tasks;
using OSPSuite.Core.Domain;
using PKSim.Core.Model;
using PKSim.Core.Repositories;
using SnapshotOntogeny = PKSim.Core.Snapshots.Ontogeny;
using ModelOntogeny = PKSim.Core.Model.Ontogeny;

namespace PKSim.Core.Snapshots.Mappers
{
   public class SnapshotContextWithSubject : SnapshotContext
   {
      public ISimulationSubject SimulationSubject { get; }

      public SnapshotContextWithSubject(ISimulationSubject simulationSubject, SnapshotContext snapshotContext) : base(snapshotContext)
      {
         SimulationSubject = simulationSubject;
      }
   }

   public class OntogenyMapper : ObjectBaseSnapshotMapperBase<ModelOntogeny, SnapshotOntogeny, SnapshotContextWithSubject>
   {
      private readonly DistributedTableFormulaMapper _distributedTableFormulaMapper;
      private readonly IOntogenyRepository _ontogenyRepository;

      public OntogenyMapper(DistributedTableFormulaMapper distributedTableFormulaMapper, IOntogenyRepository ontogenyRepository)
      {
         _distributedTableFormulaMapper = distributedTableFormulaMapper;
         _ontogenyRepository = ontogenyRepository;
      }

      public override async Task<SnapshotOntogeny> MapToSnapshot(ModelOntogeny ontogeny)
      {
         if (ontogeny.IsUndefined())
            return null;

         var snapshot = await SnapshotFrom(ontogeny);

         if (ontogeny is UserDefinedOntogeny userDefinedOntogeny)
            snapshot.Table = await _distributedTableFormulaMapper.MapToSnapshot(userDefinedOntogeny.Table);
         else
            //we do not save database description
            snapshot.Description = null;

         return snapshot;
      }

      public override async Task<ModelOntogeny> MapToModel(SnapshotOntogeny snapshot, SnapshotContextWithSubject snapshotContextContext)
      {
         if (snapshot == null)
            return new NullOntogeny();

         var speciesName = snapshotContextContext.SimulationSubject.Species.Name;
         if (snapshot.Table == null)
            return _ontogenyRepository.AllFor(speciesName).FindByName(snapshot.Name);

         var ontogeny = new UserDefinedOntogeny
         {
            Table = await _distributedTableFormulaMapper.MapToModel(snapshot.Table, snapshotContextContext),
            SpeciesName = speciesName
         };

         MapSnapshotPropertiesToModel(snapshot, ontogeny);
         return ontogeny;
      }
   }
}