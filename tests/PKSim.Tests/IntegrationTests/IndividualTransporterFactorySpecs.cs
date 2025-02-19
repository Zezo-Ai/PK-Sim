﻿using OSPSuite.BDDHelper;
using OSPSuite.BDDHelper.Extensions;
using OSPSuite.Utility.Extensions;
using PKSim.Core;
using PKSim.Core.Model;
using PKSim.Core.Services;
using PKSim.Infrastructure;

namespace PKSim.IntegrationTests
{
   public abstract class concern_for_IndividualTransporterFactory : ContextForIntegration<IIndividualTransporterFactory>
   {
      protected Individual _individual;

      public override void GlobalContext()
      {
         base.GlobalContext();
         _individual = DomainFactoryForSpecs.CreateStandardIndividual();
      }
   }

   public class When_creating_an_undefined_liver_transport_for_a_given_individual : concern_for_IndividualTransporterFactory
   {
      private IndividualTransporter _undefined;

      protected override void Because()
      {
         _undefined = sut.AddUndefinedLiverTransporterTo(_individual);
      }

     
      [Observation]
      public void should_add_the_relative_expression_to_periportal_and_pericentral_and_set_the_value_to_1()
      {
         var allTransporterContainers = _individual.AllMoleculeContainersFor<TransporterExpressionContainer>(_undefined);
         allTransporterContainers.Count.ShouldBeEqualTo(2);
         allTransporterContainers.Each(x=>x.RelativeExpressionParameter.Value.ShouldBeEqualTo(1));
      }
   }
}