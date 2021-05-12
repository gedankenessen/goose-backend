﻿using Goose.API.Repositories;
using Goose.API.Services.Issues;
using Goose.API.Utils;
using Goose.API.Utils.Exceptions;
using Goose.Domain.Models.Projects;
using Goose.Domain.Models.Issues;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Goose.API.Authorization;
using Goose.API.Authorization.Requirements;
using MongoDB.Bson;
using Microsoft.AspNetCore.Http;
using Goose.API.Utils.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace Goose.API.Services.issues
{
    public interface IIssueSummaryService
    {
        public Task<IList<IssueRequirement>> CreateSummary(string issueId);
        public Task<IList<IssueRequirement>> GetSummary(string issueId);
        public Task AcceptSummary(string issueId);
        public Task DeclineSummary(string issueId);
    }

    public class IssueSummaryService : IIssueSummaryService
    {
        private readonly IIssueRepository _issueRepository;
        private readonly IIssueRequirementService _issueRequirementService;
        private readonly IStateService _stateService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IProjectRepository _projectRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly IHttpContextAccessor _contextAccessor;

        public IssueSummaryService(
            IIssueRepository issueRepository,
            IIssueRequirementService issueRequirementService,
            IStateService stateService,
            IHttpContextAccessor httpContextAccessor, IProjectRepository projectRepository, IHttpContextAccessor contextAccessor,
            IAuthorizationService authorizationService)
        {
            _issueRepository = issueRepository;
            _issueRequirementService = issueRequirementService;
            _stateService = stateService;
            _httpContextAccessor = httpContextAccessor;
            _projectRepository = projectRepository;
            _contextAccessor = contextAccessor;
            _authorizationService = authorizationService;
        }

        public async Task AcceptSummary(string issueId)
        {
            var issue = await _issueRepository.GetAsync(issueId.ToObjectId());
            if (!(await _stateService.GetState(issue.ProjectId, issue.StateId)).Phase.Equals(State.NegotiationPhase))
                throw new HttpStatusException(StatusCodes.Status400BadRequest, "You cannot accept a summary outside of the negotiation phase");

            if (!await _authorizationService.HasAtLeastOneRequirement(_contextAccessor.HttpContext.User, await _projectRepository.GetAsync(issue.ProjectId),
                ProjectRolesRequirement.CustomerRequirement))
                throw new HttpStatusException(StatusCodes.Status403Forbidden,
                    $"the user {_contextAccessor.HttpContext.User.GetUserId()} is not a customer of this project");

            if (issue is null)
                throw new HttpStatusException(400, "Das angefragte Issue Existiert nicht");

            if (issue.IssueDetail.RequirementsSummaryCreated is false)
                throw new HttpStatusException(400, "Es wurde noch kein Zusammenfassung für dieses Ticket erstellt");

            issue.IssueDetail.RequirementsAccepted = true;
            var states = await _stateService.GetStates(issue.ProjectId);

            if (states is null)
                throw new HttpStatusException(400, "Es wurden keine Statuse für dieses Project gefunden");

            var state = states.FirstOrDefault(x => x.Name.Equals(State.WaitingState));

            if (state is null)
                throw new HttpStatusException(400, "Es wurde kein State gefunden");

            issue.IssueDetail.RequirementsAccepted = true;
            issue.StateId = state.Id;
            issue.ConversationItems.Add(new IssueConversation()
            {
                Id = ObjectId.GenerateNewId(),
                CreatorUserId = _httpContextAccessor.HttpContext.User.GetUserId(),
                Type = IssueConversation.SummaryAcceptedType,
                Data = "",
                Requirements = issue.IssueDetail.Requirements.Select(x => x.Requirement).ToList(),
            });

            issue.ConversationItems.Add(new IssueConversation()
            {
                Id = ObjectId.GenerateNewId(),
                CreatorUserId = _httpContextAccessor.HttpContext.User.GetUserId(),
                Type = IssueConversation.StateChangeType,
                Data = $"Status von {State.NegotiationState} zu {State.WaitingState} geändert.",
            });
            await _issueRepository.UpdateAsync(issue);
        }

        public async Task<IList<IssueRequirement>> CreateSummary(string issueId)
        {
            var issue = await _issueRepository.GetAsync(issueId.ToObjectId());
            if (!(await _stateService.GetState(issue.ProjectId, issue.StateId)).Phase.Equals(State.NegotiationPhase))
                throw new HttpStatusException(StatusCodes.Status400BadRequest, "You cannot create a summary outside of the negotiation phase");

            if (!await _authorizationService.HasAtLeastOneRequirement(_contextAccessor.HttpContext.User, await _projectRepository.GetAsync(issue.ProjectId),
                CompanyRolesRequirement.CompanyOwner, ProjectRolesRequirement.EmployeeRequirement, ProjectRolesRequirement.LeaderRequirement))
                throw new HttpStatusException(StatusCodes.Status403Forbidden,
                    $"the user {_contextAccessor.HttpContext.User.GetUserId()} is not a company or employee of this project");

            if (issue is null)
                throw new HttpStatusException(400, "Das angefragte Issue Existiert nicht");

            if (issue.IssueDetail.Requirements is null)
                throw new HttpStatusException(400, "Die Requirements waren null");

            if (issue.IssueDetail.Requirements.Count <= 0 && issue.IssueDetail.ExpectedTime <= 0)
                throw new HttpStatusException(400,
                    "Um eine Zusammenfassung erstellen zu können muss mindestens eine Anforderung oder eine geschätze Zeit vorhanden sein");

            issue.IssueDetail.RequirementsSummaryCreated = true;
            issue.ConversationItems.Add(new IssueConversation()
            {
                Id = ObjectId.GenerateNewId(),
                CreatorUserId = _httpContextAccessor.HttpContext.User.GetUserId(),
                Type = IssueConversation.SummaryCreatedType,
                Data = "",
                Requirements = issue.IssueDetail.Requirements.Select(x => x.Requirement).ToList(),
            });
            await _issueRepository.UpdateAsync(issue);

            return await _issueRequirementService.GetAllOfIssueAsync(issueId.ToObjectId());
        }

        public async Task DeclineSummary(string issueId)
        {
            var issue = await _issueRepository.GetAsync(issueId.ToObjectId());
            if (!(await _stateService.GetState(issue.ProjectId, issue.StateId)).Phase.Equals(State.NegotiationPhase))
                throw new HttpStatusException(StatusCodes.Status400BadRequest, "You cannot decline a summary outside of the negotiation phase");

            if (!await _authorizationService.HasAtLeastOneRequirement(_contextAccessor.HttpContext.User, await _projectRepository.GetAsync(issue.ProjectId),
                ProjectRolesRequirement.CustomerRequirement))
                throw new HttpStatusException(StatusCodes.Status403Forbidden,
                    $"the user {_contextAccessor.HttpContext.User.GetUserId()} is not a customer of this project");

            if (issue is null)
                throw new HttpStatusException(400, "Das angefragte Issue Existiert nicht");

            if (issue.IssueDetail.RequirementsSummaryCreated is false)
                throw new HttpStatusException(400, "Es wurde noch keine Zusammenfassung erstellt und kann deswegen nicht abgelehnt werden");

            if (issue.IssueDetail.RequirementsAccepted)
                throw new HttpStatusException(400, "Die Zusammenfassung wurde schon angenommen und kann nicht abgelehnt werden");

            issue.IssueDetail.RequirementsSummaryCreated = false;
            issue.ConversationItems.Add(new IssueConversation()
            {
                Id = ObjectId.GenerateNewId(),
                CreatorUserId = _httpContextAccessor.HttpContext.User.GetUserId(),
                Type = IssueConversation.SummaryDeclinedType,
                Data = "",
                Requirements = issue.IssueDetail.Requirements.Select(x => x.Requirement).ToList(),
            });
            await _issueRepository.UpdateAsync(issue);
        }

        public async Task<IList<IssueRequirement>> GetSummary(string issueId)
        {
            var issue = await _issueRepository.GetAsync(issueId.ToObjectId());
            var project = await _projectRepository.GetAsync(issue.ProjectId);
            if (!await _authorizationService.HasAtLeastOneRequirement(_contextAccessor.HttpContext.User, project,
                CompanyRolesRequirement.CompanyOwner, ProjectRolesRequirement.CustomerRequirement, ProjectRolesRequirement.EmployeeRequirement,
                ProjectRolesRequirement.ReadonlyEmployeeRequirement, ProjectRolesRequirement.LeaderRequirement))
                throw new HttpStatusException(StatusCodes.Status403Forbidden,
                    $"the user {_contextAccessor.HttpContext.User.GetUserId()} does not have a role in this project");


            if (issue is null)
                throw new HttpStatusException(400, "Das angefragte Issue Existiert nicht");

            if (issue.IssueDetail.RequirementsSummaryCreated is false)
                throw new HttpStatusException(400, "Die Zusammenfassung wurde noch nicht erstellt");

            return await _issueRequirementService.GetAllOfIssueAsync(issueId.ToObjectId());
        }
    }
}