﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Goose.API;
using Goose.API.Repositories;
using Goose.API.Services.Issues;
using Goose.Domain.DTOs;
using Goose.Domain.DTOs.Issues;
using Goose.Domain.Models.Auth;
using Goose.Domain.Models.Identity;
using Goose.Domain.Models.Issues;
using Goose.Domain.Models.Projects;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;

namespace Goose.Tests.Application.IntegrationTests
{
    //TODO wo wird der client header gesetzt
    //TODO GenerateUserAndSetToProject generiert einen user. Solen seine rollen wirklich als Projekt und CompanyRolle gesetzt werden?
    public class NewTestHelper : IDisposable
    {
        private readonly HttpClient _client;

        private SignInResponse LoggedInUser { get; set; }
        private List<ObjectId> _companies = new();
        private List<ObjectId> _projects = new();
        private List<ObjectId> _issues = new();


        private ICompanyRepository CompanyRepository { get; }
        private IUserRepository UserRepository { get; }
        private IIssueRepository IssueRepository { get; }
        private IRoleRepository RoleRepository { get; }
        private IProjectRepository ProjectRepository { get; }
        private IIssueRequirementService IssueRequirementService { get; }

        public NewTestHelper(HttpClient client)
        {
            this._client = client;

            var factory = new WebApplicationFactory<Startup>();
            var scopeFactory = factory.Server.Services.GetService<IServiceScopeFactory>();

            using var scope = scopeFactory.CreateScope();
            CompanyRepository = scope.ServiceProvider.GetService<ICompanyRepository>();
            UserRepository = scope.ServiceProvider.GetService<IUserRepository>();
            ProjectRepository = scope.ServiceProvider.GetService<IProjectRepository>();
            RoleRepository = scope.ServiceProvider.GetService<IRoleRepository>();
            IssueRepository = scope.ServiceProvider.GetService<IIssueRepository>();
            IssueRequirementService = scope.ServiceProvider.GetService<IIssueRequirementService>();
        }

        #region Generate

        public async Task<HttpResponseMessage> GenerateCompany()
        {
            SignUpRequest signUpRequest = new SignUpRequest()
            {
                Firstname = $"{new Random().NextDouble()}", Lastname = $"{new Random().NextDouble()}", CompanyName = $"{new Random().NextDouble()}",
                Password = "Test12345"
            };
            return await GenerateCompany(signUpRequest);
        }

        public async Task<HttpResponseMessage> GenerateCompany(SignUpRequest signUpRequest)
        {
            return await CreateCompany(signUpRequest);
        }

        public async Task<HttpResponseMessage> GenerateProject(ObjectId companyId)
        {
            return await GenerateProject(companyId, new ProjectDTO {Name = $"{new Random().NextDouble()}"});
        }

        public async Task<HttpResponseMessage> GenerateProject(ObjectId companyId, ProjectDTO projectDto)
        {
            return await CreateProject(companyId, projectDto);
        }

        public async Task<HttpResponseMessage> GenerateTimeSheet(ObjectId issueId, ObjectId userId)
        {
            return await CreateTimeSheet(issueId, new IssueTimeSheetDTO()
            {
                User = new UserDTO() {Id = userId}
            });
        }

        public async Task AddUserToProject(ObjectId projectId, ObjectId userId, params Role[] roles)
        {
            var user = await UserRepository.GetAsync(userId);
            await AddUserToProject(projectId, user, roles);
        }

        private async Task AddUserToProject(ObjectId projectId, User user, params Role[] roles)
        {
            var uri = $"api/projects/{projectId}/users/{user.Id}";
            var addRequest = new PropertyUserDTO()
            {
                User = new UserDTO(user),
                Roles = roles.Select(it => new RoleDTO(it)).ToList()
            };
            await _client.PutAsync(uri, addRequest.ToStringContent());
        }


        public async Task<ObjectId> GenerateUserAndSetToProject(ObjectId companyId, ObjectId projectId,
            params Role[] roles)
        {
            var login = new PropertyUserLoginDTO
            {
                Firstname = $"{new Random().NextDouble()}",
                Lastname = $"{new Random().NextDouble()}",
                Password = "Test12345",
                Roles = roles.Select(it => new RoleDTO(it)).ToList()
            };
            return await GenerateUserAndSetToProject(companyId, projectId, login, roles);
        }

        public async Task<ObjectId> GenerateUserAndSetToProject(ObjectId companyId, ObjectId projectId, PropertyUserLoginDTO user,
            params Role[] roles)
        {
            //generate User 
            var newUser = await CreateUserForCompany(user, companyId);

            //Add User to Project with Role
            await AddUserToProject(projectId, newUser.User.Id, roles);

            //Sign In with new User
            var signInResult = await SignIn(new SignInRequest
            {
                Username = newUser.User.Username,
                Password = user.Password
            });

            _client.Auth(signInResult);
            return signInResult.User.Id;
        }

        public async Task<HttpResponseMessage> GenerateIssue(UserDTO user, ProjectDTO project, Action<IssueDTO> withIssue = null)
        {
            var issue = await CreateDefaultIssue(user, project);
            withIssue?.Invoke(issue);
            return await GenerateIssue(project, issue);
        }

        public async Task<HttpResponseMessage> GenerateIssue(ProjectDTO project, IssueDTO issue)
        {
            return await CreateIssue(project.Id, issue);
        }

        #endregion

        #region Requests

        public async Task<HttpResponseMessage> CreateCompany(SignUpRequest signUpRequest)
        {
            var uri = "/api/auth/signUp";
            var response = await _client.PostAsync(uri, signUpRequest.ToStringContent());
            var res = await response.Content.Parse<SignInResponse>();
            _companies.AddRange(res.Companies.Select(it => it.Id));
            return response;
        }

        public async Task<HttpResponseMessage> CreateProject(ObjectId companyId, ProjectDTO projectDto)
        {
            var uri = $"api/companies/{companyId}/projects";
            var res = await _client.PostAsync(uri, projectDto.ToStringContent());
            _projects.Add((await res.Parse<ProjectDTO>()).Id);
            return res;
        }

        public async Task<HttpResponseMessage> CreateTimeSheet(ObjectId issueId, IssueTimeSheetDTO dto)
        {
            var uri = $"api/issues/{issueId}/timesheets/";
            return await _client.PostAsync(uri, dto.ToStringContent());
        }

        public async Task<SignInResponse> CreateUserForCompany(PropertyUserLoginDTO login, ObjectId companyId)
        {
            var company = await CompanyRepository.GetAsync(companyId);
            var uri = $"/api/companies/{company.Id}/users";
            var response = await _client.PostAsync(uri, login.ToStringContent());
            return await response.Content.Parse<SignInResponse>();
        }

        public async Task<HttpResponseMessage> CreateIssue(ObjectId projectId, IssueDTO issue)
        {
            var uri = $"api/projects/{projectId}/issues/";
            var res = await _client.PostAsync(uri, issue.ToStringContent());
            try
            {
                _issues.Add((await res.Parse<IssueDTO>()).Id);
            }
            catch (Exception)
            {
            }

            return res;
        }

        public async Task<HttpResponseMessage> CreateRequirement(ObjectId issueId, IssueRequirement requirement)
        {
            var uri = $"/api/issues/{issueId}/requirements/";
            return await _client.PostAsync(uri, requirement.ToStringContent());
        }

        public async Task<SignInResponse> SignIn(SignInRequest signInRequest)
        {
            var uri = "/api/auth/signIn";
            var response = await _client.PostAsync(uri, signInRequest.ToStringContent());
            return await response.Content.Parse<SignInResponse>();
        }

        #endregion

        #region GetRequests

        public async Task<Issue> GetIssueAsync(ObjectId issueId)
        {
            return await IssueRepository.GetAsync(issueId);
        }

        public async Task<IssueDTODetailed> GetIssueThroughClientAsync(ObjectId projectId, ObjectId issueId)
        {
            var uri = $"api/projects/{projectId}/issues/{issueId}?GetAll=true";
            return await _client.GetAsync(uri).Parse<IssueDTODetailed>();
        }

        public async Task<IssueDTODetailed> GetIssueThroughClientAsync(IssueDTO issueDto)
        {
            return await GetIssueThroughClientAsync(issueDto.Project.Id, issueDto.Id);
        }

        private async Task<IList<StateDTO>> GetStateListAsync(ObjectId projectId)
        {
            var uri = $"api/projects/{projectId}/states";
            var responce = await _client.GetAsync(uri);
            return await responce.Content.Parse<IList<StateDTO>>();
        }

        public async Task<StateDTO> GetStateByNameAsync(ObjectId projectId, string name)
        {
            var list = await GetStateListAsync(projectId);
            return list.FirstOrDefault(x => x.Name.Equals(name));
        }

        public async Task<StateDTO> GetStateById(ObjectId projectId, ObjectId Id)
        {
            return (await GetStateListAsync(projectId)).FirstOrDefault(x => x.Id.Equals(Id));
        }

        #endregion

        #region Utils

        public async Task<IssueDTO> CreateDefaultIssue(UserDTO user, ProjectDTO projectDto)
        {
            return new IssueDTO
            {
                Author = user,
                Client = user,
                Project = projectDto,
                State = await GetStateByNameAsync(projectDto.Id, State.NegotiationState),
                IssueDetail = new IssueDetail
                {
                    Name = $"{new Random().NextDouble()}",
                    Type = Issue.TypeFeature,
                    StartDate = default,
                    EndDate = default,
                    ExpectedTime = 0,
                    Progress = 0,
                    Description = null,
                    Requirements = null,
                    RequirementsAccepted = false,
                    RequirementsSummaryCreated = false,
                    RequirementsNeeded = true,
                    Priority = 0,
                    Visibility = false,
                    RelevantDocuments = null
                }
            };
        }

        #endregion

        #region Clearup

        public async Task ClearCompany(ObjectId companyId)
        {
            var company = await CompanyRepository.GetAsync(companyId);
            if (company != null)
            {
                await Task.WhenAll(company.Users.Select(it => UserRepository.DeleteAsync(it.UserId)));
                await CompanyRepository.DeleteAsync(company.Id);
            }
        }

        public async Task ClearCompanies()
        {
            await Task.WhenAll(_companies.Select(ClearCompany));
        }

        public async Task ClearProject(ObjectId projectId)
        {
            await ProjectRepository.DeleteAsync(projectId);
        }

        public async Task ClearProjects()
        {
            await Task.WhenAll(_projects.Select(ClearProject));
        }

        public async Task ClearIssue(ObjectId issueId)
        {
            await IssueRepository.DeleteAsync(issueId);
        }

        public async Task ClearIssues()
        {
            await Task.WhenAll(_issues.Select(ClearIssue));
        }

        public void ClearAll()
        {
            Dispose();
        }

        public async void Dispose()
        {
            CompanyRepository?.Dispose();
            UserRepository?.Dispose();
            IssueRepository?.Dispose();
            RoleRepository?.Dispose();
            ProjectRepository?.Dispose();

            await ClearIssues();
            await ClearProjects();
            await ClearCompanies();
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    public static class TestHelperExtensions
    {
        public static void Auth(this HttpClient client, SignInResponse signIn)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signIn.Token);
        }
    }
}