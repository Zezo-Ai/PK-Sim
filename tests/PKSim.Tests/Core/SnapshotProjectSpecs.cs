﻿using OSPSuite.BDDHelper;
using OSPSuite.BDDHelper.Extensions;
using OSPSuite.Core.Domain;
using PKSim.Core.Snapshots;
using Event = PKSim.Core.Snapshots.Event;
using SnapshotProject = PKSim.Core.Snapshots.Project;
using ExpressionProfile = PKSim.Core.Snapshots.ExpressionProfile;

namespace PKSim.Core
{
   public abstract class concern_for_SnapshotProject : ContextSpecification<SnapshotProject>
   {
      protected Individual _individual;
      protected Compound _compound;
      protected Formulation _formulation;
      protected Protocol _protocol;
      protected Population _population;
      protected Event _event;
      protected ExpressionProfile _expressionProfile;

      protected override void Context()
      {
         _individual = new Individual().WithName("Individual");
         _compound = new Compound().WithName("Compound");
         _formulation = new Formulation().WithName("Formulation");
         _protocol = new Protocol().WithName("Protocol");
         _population = new Population().WithName("Population");
         _event = new Event().WithName("Event");
         _expressionProfile = new ExpressionProfile
         {
            Species = "Human",
            Molecule = "E1",
            Category = "Healthy",
         };

         sut = new SnapshotProject
         {
            Compounds = new[] {_compound},
            Formulations = new[] {_formulation},
            Protocols = new[] {_protocol},
            Individuals = new[] {_individual},
            Populations = new[] {_population},
            Events = new[] {_event},
            ExpressionProfiles = new[] {_expressionProfile}
         };
      }
   }

   public class When_retrieving_building_blocks_by_type : concern_for_SnapshotProject
   {
      [Observation]
      public void should_return_the_expected_building_blocks_for_the_basic_types()
      {
         sut.BuildingBlocksByType(PKSimBuildingBlockType.Compound).ShouldBeEqualTo(sut.Compounds);
         sut.BuildingBlocksByType(PKSimBuildingBlockType.Formulation).ShouldBeEqualTo(sut.Formulations);
         sut.BuildingBlocksByType(PKSimBuildingBlockType.Protocol).ShouldBeEqualTo(sut.Protocols);
         sut.BuildingBlocksByType(PKSimBuildingBlockType.Individual).ShouldBeEqualTo(sut.Individuals);
         sut.BuildingBlocksByType(PKSimBuildingBlockType.Population).ShouldBeEqualTo(sut.Populations);
         sut.BuildingBlocksByType(PKSimBuildingBlockType.Event).ShouldBeEqualTo(sut.Events);
         sut.BuildingBlocksByType(PKSimBuildingBlockType.ExpressionProfile).ShouldBeEqualTo(sut.ExpressionProfiles);
      }
   }

   public class When_retrieving_building_block_by_type_and_name : concern_for_SnapshotProject
   {
      [Observation]
      public void should_return_the_expected_building_block_if_it_exists()
      {
         sut.BuildingBlockByTypeAndName(PKSimBuildingBlockType.Compound, _compound.Name).ShouldBeEqualTo(_compound);
         sut.BuildingBlockByTypeAndName(PKSimBuildingBlockType.Formulation, _formulation.Name).ShouldBeEqualTo(_formulation);
         sut.BuildingBlockByTypeAndName(PKSimBuildingBlockType.Protocol, _protocol.Name).ShouldBeEqualTo(_protocol);
         sut.BuildingBlockByTypeAndName(PKSimBuildingBlockType.Individual, _individual.Name).ShouldBeEqualTo(_individual);
         sut.BuildingBlockByTypeAndName(PKSimBuildingBlockType.Population, _population.Name).ShouldBeEqualTo(_population);
         sut.BuildingBlockByTypeAndName(PKSimBuildingBlockType.Event, _event.Name).ShouldBeEqualTo(_event);
         sut.BuildingBlockByTypeAndName(PKSimBuildingBlockType.ExpressionProfile, "E1|Human|Healthy").ShouldBeEqualTo(_expressionProfile);
      }

      [Observation]
      public void should_return_null_if_the_building_block_does_not_exist()
      {
         sut.BuildingBlockByTypeAndName(PKSimBuildingBlockType.Compound, "NOPE").ShouldBeNull();
         sut.BuildingBlockByTypeAndName(PKSimBuildingBlockType.ExpressionProfile, "E1|Rat|Healthy").ShouldBeNull();
      }
   }

   public class When_swapping_a_building_block_for_another_building_block_that_does_not_exist_by_type_and_name : concern_for_SnapshotProject
   {
      [Observation]
      public void should_throw_an_exception()
      {
         The.Action(() => sut.Swap(new Individual().WithName("TOTO"))).ShouldThrowAn<PKSimException>();
      }
   }

   public class When_swapping_a_building_block_for_another_building_block : concern_for_SnapshotProject
   {
      private Individual _otherIndividual;
      private Individual _newIndividual;

      protected override void Context()
      {
         base.Context();
         _otherIndividual = new Individual().WithName("OTHER");
         _newIndividual = new Individual().WithName(_individual.Name);
         sut.Individuals = new[] {_individual, _otherIndividual};
      }

      protected override void Because()
      {
         sut.Swap(_newIndividual);
      }

      [Observation]
      public void should_replace_the_building_block_in_the_list_of_typed_building_blocks()
      {
         sut.Individuals.ShouldOnlyContainInOrder(_newIndividual, _otherIndividual);
         sut.BuildingBlockByTypeAndName(_newIndividual.BuildingBlockType, _newIndividual.Name).ShouldBeEqualTo(_newIndividual);
      }
   }

   public class When_swapping_an_expression_profile_building_block_for_another_building_block : concern_for_SnapshotProject
   {
      private ExpressionProfile _otherExpressionProfile;
      private ExpressionProfile _newExpressionProfile;
      private ExpressionProfile _oldExpressionProfile;

      protected override void Context()
      {
         base.Context();
         _otherExpressionProfile = new ExpressionProfile {Molecule = "OTHER", Species = "Human", Category = "Cat"};
         _newExpressionProfile = new ExpressionProfile { Molecule = "MOL", Species = "Human", Category = "Cat" };
         _oldExpressionProfile = new ExpressionProfile { Molecule = "MOL", Species = "Human", Category = "Cat" };
         sut.ExpressionProfiles = new[] { _oldExpressionProfile, _otherExpressionProfile };
      }

      protected override void Because()
      {
         sut.Swap(_newExpressionProfile);
      }

      [Observation]
      public void should_replace_the_building_block_in_the_list_of_typed_building_blocks()
      {
         sut.ExpressionProfiles.ShouldOnlyContainInOrder(_newExpressionProfile, _otherExpressionProfile);
      }
   }
}