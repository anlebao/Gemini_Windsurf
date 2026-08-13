using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using VanAn.Shared.Domain;
using VanAn.Shared.Domain.Common;
using VanAn.CoreHub.Services;
using PermissionGroup = VanAn.Shared.Domain.Aggregates.UserAggregate.PermissionGroup;
using UserRole = VanAn.Shared.Domain.Aggregates.UserAggregate.UserRole;

namespace VanAn.ShopERP.Components.Pages.Admin
{
    public partial class PermissionGroupManagement : ComponentBase
    {
        private List<PermissionGroup> _groups = new();
        private bool _isLoading = true;
        private bool _isSaving = false;
        private bool _showCreateModal = false;
        private bool _showRolesModal = false;
        private PermissionGroup? _selectedGroup;
        private string _alertMessage = string.Empty;
        private string _alertType = "info";
        private readonly CreateGroupForm _createForm = new();

        protected override async Task OnInitializedAsync()
        {
            await LoadGroups();
        }

        private async Task LoadGroups()
        {
            _isLoading = true;
            try
            {
                var tenantId = GetTenantId();
                _groups = (await GroupService.ListGroupsAsync(tenantId)).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load permission groups");
                ShowAlert("Không thể tải danh sách nhóm quyền.", "error");
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
            if (string.IsNullOrWhiteSpace(_createForm.Name))
            {
                ShowAlert("Tên nhóm không được để trống.", "error");
                return;
            }

            _isSaving = true;
            try
            {
                var tenantId = GetTenantId();
                await GroupService.CreateGroupAsync(tenantId, _createForm.Name, _createForm.Description);
                ShowAlert("Nhóm quyền đã được tạo thành công.", "success");
                CloseCreateModal();
                await LoadGroups();
            }
            catch (InvalidOperationException ex)
            {
                ShowAlert(ex.Message, "error");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to create permission group");
                ShowAlert("Không thể tạo nhóm quyền.", "error");
            }
            finally
            {
                _isSaving = false;
            }
        }

        private void OpenEditRolesModal(PermissionGroup group)
        {
            _selectedGroup = group;
            _showRolesModal = true;
        }

        private void CloseRolesModal()
        {
            _showRolesModal = false;
            _selectedGroup = null;
        }

        private async Task ToggleRole(UserRole role, bool isChecked)
        {
            if (_selectedGroup is null) return;

            _isSaving = true;
            try
            {
                var tenantId = GetTenantId();
                if (isChecked)
                {
                    await GroupService.AddRoleToGroupAsync(_selectedGroup.Id, tenantId, role);
                    ShowAlert($"Đã thêm vai trò {role}.", "success");
                }
                else
                {
                    await GroupService.RemoveRoleFromGroupAsync(_selectedGroup.Id, tenantId, role);
                    ShowAlert($"Đã xoá vai trò {role}.", "success");
                }
                await LoadGroups();
                _selectedGroup = _groups.FirstOrDefault(g => g.Id == _selectedGroup.Id);
            }
            catch (InvalidOperationException ex)
            {
                ShowAlert(ex.Message, "error");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to toggle role");
                ShowAlert("Không thể cập nhật vai trò.", "error");
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
            if (TenantProvider.TenantId != Guid.Empty)
                return new TenantId(TenantProvider.TenantId);

            // Fallback to config-driven default tenant (dev/demo)
            var tenantIdStr = Configuration["Seed:TenantId"] ?? "00000000-0000-0000-0000-000000000001";
            return new TenantId(Guid.Parse(tenantIdStr));
        }

        private class CreateGroupForm
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;

            public void Reset()
            {
                Name = string.Empty;
                Description = string.Empty;
            }
        }
    }
}
