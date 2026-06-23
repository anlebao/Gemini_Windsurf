using Microsoft.AspNetCore.Components;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Services;
using DemoUser = VanAn.Shared.Domain.Aggregates.UserAggregate.DemoUser;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.ShopERP.Components.Pages.Admin
{
    public partial class UserManagement : ComponentBase
    {
        private List<DemoUser> _users = new();
        private bool _isLoading = true;
        private bool _isSaving = false;
        private bool _showCreateModal = false;
        private bool _showDeactivateModal = false;
        private bool _showRoleModal = false;
        private DemoUser? _selectedUser;
        private string _alertMessage = string.Empty;
        private string _alertType = "info";
        private UserRole _roleToAssign = UserRole.Staff;
        private readonly CreateUserForm _createForm = new();

        protected override async Task OnInitializedAsync()
        {
            await LoadUsers();
        }

        private async Task LoadUsers()
        {
            _isLoading = true;
            try
            {
                var tenantId = GetTenantId();
                _users = (await UserService.ListUsersAsync(tenantId)).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load users");
                ShowAlert("Không thể tải danh sách người dùng.", "error");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OpenCreateModal()
        {
            _createForm.Reset();
            _showCreateModal = true;
        }

        private void CloseCreateModal()
        {
            _showCreateModal = false;
        }

        private async Task HandleCreateSubmit()
        {
            if (string.IsNullOrWhiteSpace(_createForm.Username)
                || string.IsNullOrWhiteSpace(_createForm.Password)
                || string.IsNullOrWhiteSpace(_createForm.DisplayName))
            {
                ShowAlert("Vui lòng điền đầy đủ các trường bắt buộc.", "error");
                return;
            }

            _isSaving = true;
            try
            {
                var tenantId = GetTenantId();
                await UserService.CreateUserAsync(
                    tenantId,
                    _createForm.Username,
                    _createForm.Password,
                    _createForm.DisplayName,
                    _createForm.Role);
                ShowAlert("Người dùng đã được tạo thành công.", "success");
                CloseCreateModal();
                await LoadUsers();
            }
            catch (InvalidOperationException ex)
            {
                ShowAlert(ex.Message, "error");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create user");
                ShowAlert("Không thể tạo người dùng.", "error");
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void OpenDeactivateModal(DemoUser user)
        {
            _selectedUser = user;
            _showDeactivateModal = true;
        }

        private void CloseDeactivateModal()
        {
            _showDeactivateModal = false;
            _selectedUser = null;
        }

        private async Task ConfirmDeactivate()
        {
            if (_selectedUser is null) return;
            _isSaving = true;
            try
            {
                await UserService.DeactivateUserAsync(_selectedUser.Id, GetTenantId());
                ShowAlert("Người dùng đã bị vô hiệu hoá.", "success");
                CloseDeactivateModal();
                await LoadUsers();
            }
            catch (InvalidOperationException ex)
            {
                ShowAlert(ex.Message, "error");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to deactivate user");
                ShowAlert("Không thể vô hiệu hoá người dùng.", "error");
            }
            finally
            {
                _isSaving = false;
            }
        }

        private async Task ReactivateUser(DemoUser user)
        {
            _isSaving = true;
            try
            {
                await UserService.ReactivateUserAsync(user.Id, GetTenantId());
                ShowAlert("Người dùng đã được kích hoạt.", "success");
                await LoadUsers();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to reactivate user");
                ShowAlert("Không thể kích hoạt người dùng.", "error");
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void OpenRoleModal(DemoUser user)
        {
            _selectedUser = user;
            _roleToAssign = UserRole.Staff;
            _showRoleModal = true;
        }

        private void CloseRoleModal()
        {
            _showRoleModal = false;
            _selectedUser = null;
        }

        private async Task ConfirmAssignRole()
        {
            if (_selectedUser is null) return;
            _isSaving = true;
            try
            {
                await RoleService.AssignRoleToUserAsync(_selectedUser.Id, GetTenantId(), _roleToAssign);
                ShowAlert("Vai trò đã được gán.", "success");
                CloseRoleModal();
                await LoadUsers();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to assign role");
                ShowAlert("Không thể gán vai trò.", "error");
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void ShowAlert(string message, string type)
        {
            _alertMessage = message;
            _alertType = type;
        }

        private TenantId GetTenantId()
        {
            return TenantProvider.TenantId == Guid.Empty
                ? new TenantId(Guid.Parse("00000000-0000-0000-0000-000000000001"))
                : new TenantId(TenantProvider.TenantId);
        }

        private class CreateUserForm
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public UserRole Role { get; set; } = UserRole.Staff;

            public void Reset()
            {
                Username = string.Empty;
                Password = string.Empty;
                DisplayName = string.Empty;
                Role = UserRole.Staff;
            }
        }
    }
}
