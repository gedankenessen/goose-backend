﻿using Goose.API.Authorization.Requirements;
using Goose.API.Repositories;
using Goose.API.Utils.Exceptions;
using Goose.Domain.DTOs;
using Goose.Domain.Models.Companies;
using Goose.Domain.Models.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Goose.Domain.Models;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Goose.API.Utils.Authentication;
using System;

namespace Goose.API.Services
{
    public interface IProjectService
    {
        Task<ProjectDTO> CreateProjectAsync(ObjectId companyId, ProjectDTO requestedProject);
        Task UpdateProject(ObjectId projectId, ProjectDTO projectDTO);
        Task<IList<ProjectDTO>> GetProjects(ObjectId companyId);
        Task<ProjectDTO> GetProject(ObjectId projectId);
    }

    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly ICompanyRepository _companyRepository;
        private readonly IAuthorizationService _authorizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ProjectService(IProjectRepository projectRepository, ICompanyRepository companyRepository, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
        {
            _projectRepository = projectRepository;
            _companyRepository = companyRepository;
            _authorizationService = authorizationService;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ProjectDTO> CreateProjectAsync(ObjectId companyId, ProjectDTO requestedProject)
        {
            // is client allowed to create a project? Clients needs to have the role "companyOwner" in the provided <companyId>.
            Company company = await _companyRepository.GetAsync(companyId);

            if (company is null)
                throw new HttpStatusException(StatusCodes.Status400BadRequest, "Company not found.");

           // ALSO POSSIBLE => ...AuthorizeAsync(_httpContextAccessor.HttpContext.User, company, new[]{ CompanyRolesRequirement.CompanyOwner, CompanyRolesRequirement.CompanyCustomer })...
            if ((await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, company, CompanyRolesRequirement.CompanyOwner)).Succeeded is false)
                throw new HttpStatusException(StatusCodes.Status403Forbidden, "Missing role(s).");

            var newProject = new Project()
            {
                Id = ObjectId.GenerateNewId(),
                CompanyId = companyId,
                ProjectDetail = new ProjectDetail()
                {
                    Name = requestedProject.Name,
                },
                States = GetDefaultStates()
            };

            await _projectRepository.CreateAsync(newProject);

            return new ProjectDTO(newProject);
        }

        private IList<State> GetDefaultStates()
        {
            State CreateDefaultState(string name, string phase)
            {
                return new State()
                {
                    Id = ObjectId.GenerateNewId(),
                    Name = name,
                    Phase = phase,
                    UserGenerated = false,
                };
            }

            var states = new List<State>()
            {
                CreateDefaultState(State.CheckingState, State.NegotiationPhase),
                CreateDefaultState(State.NegotiationState, State.NegotiationPhase),

                CreateDefaultState(State.BlockedState, State.ProcessingPhase),
                CreateDefaultState(State.WaitingState, State.ProcessingPhase),
                CreateDefaultState(State.ProcessingState, State.ProcessingPhase),
                CreateDefaultState(State.ReviewState, State.ProcessingPhase),

                CreateDefaultState(State.CompletedState, State.ConclusionPhase),
                CreateDefaultState(State.CancelledState, State.ConclusionPhase),
                CreateDefaultState(State.ArchivedState, State.ConclusionPhase),
            };

            return states;
        }

        public async Task<ProjectDTO> GetProject(ObjectId projectId)
        {
            var project = await _projectRepository.GetAsync(projectId);

            var userId = _httpContextAccessor.HttpContext.User.GetUserId();

            var isInProject = project.Users?.Any(x => x.UserId.Equals(userId)) != null;

            if(!isInProject)
                throw new HttpStatusException(StatusCodes.Status403Forbidden, "Sie haben kein Zugriff auf dieses Projekt");

            return new ProjectDTO(project);
        }

        public async Task<IList<ProjectDTO>> GetProjects(ObjectId companyId)
        {
            var projects = await _projectRepository.FilterByAsync(x => x.CompanyId == companyId);

            var userId = _httpContextAccessor.HttpContext.User.GetUserId();

            var projectList = projects.Where(x => x.Users.Any(u => u.UserId.Equals(userId)));

            var projectDTOs = from project in projectList
                              select new ProjectDTO(project);

            return projectDTOs.ToList();
        }

        public async Task UpdateProject(ObjectId projectId, ProjectDTO projectDTO)
        {
            await _projectRepository.UpdateProject(projectId, projectDTO.Name);
        }
    }
}
