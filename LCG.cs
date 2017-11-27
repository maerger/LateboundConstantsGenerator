﻿using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using XrmToolBox.Extensibility;

namespace Rappen.XTB.LCG
{
    public partial class LCG : PluginControlBase
    {
        #region Private Fields

        private List<EntityMetadataProxy> entities;
        private Dictionary<string, int> groupBoxHeights;
        private EntityMetadataProxy selectedEntity;

        #endregion Private Fields

        #region Public Constructors

        public LCG()
        {
            IEnumerable<Control> GetAll(Control control, Type type)
            {
                var controls = control.Controls.Cast<Control>();
                return controls.SelectMany(ctrl => GetAll(ctrl, type))
                                          .Concat(controls)
                                          .Where(c => c.GetType() == type);
            }

            InitializeComponent();
            groupBoxHeights = new Dictionary<string, int>();
            foreach (var gb in GetAll(this, typeof(GroupBox)))
            {
                groupBoxHeights.Add(gb.Name, gb.Height);
            }
        }

        #endregion Public Constructors

        #region Public Methods

        public override void ClosingPlugin(PluginCloseInfo info)
        {
            SettingsSave(ConnectionDetail?.ConnectionName);
            base.ClosingPlugin(info);
        }

        #endregion Public Methods

        #region Private Event Handlers

        private void attributeFilter_Changed(object sender, EventArgs e)
        {
            FilterAttributes(selectedEntity);
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            CSharpUtils.GenerateClasses(entities, txtNamespace.Text);
        }

        private void btnLoadEntities_Click(object sender, EventArgs e)
        {
            ExecuteMethod(LoadEntities);
        }

        private void btnOutputFolder_Click(object sender, EventArgs e)
        {
            var fldr = new FolderBrowserDialog
            {
                Description = "Select folder where generated constant files will be generated.",
                SelectedPath = txtOutputFolder.Text,
                ShowNewFolderButton = true
            };
            if (fldr.ShowDialog() == DialogResult.OK)
            {
                txtOutputFolder.Text = fldr.SelectedPath;
            }
        }

        private void chkAllRows_CheckedChanged(object sender, EventArgs e)
        {
            var chk = sender as CheckBox;
            var grid = chk == chkEntAll ? gridEntities : chk == chkAttAll ? gridAttributes : null;
            if (grid != null)
            {
                foreach (DataGridViewRow row in grid.Rows)
                {
                    var metadata = row.DataBoundItem as MetadataProxy;
                    if (metadata != null)
                    {
                        metadata.SetSelected(chk.Checked);
                    }
                }
            }
        }

        private void entityFilter_Changed(object sender, EventArgs e)
        {
            FilterEntities();
            SetNamespace();
        }

        private void grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid != null && e.ColumnIndex == 0 && e.RowIndex >= 0)
            {
                var row = grid.Rows[e.RowIndex];
                var metadata = row.DataBoundItem as MetadataProxy;
                if (metadata != null)
                {
                    metadata.SetSelected(!metadata.IsSelected);
                }
            }
        }

        private void gridAttributes_Move(object sender, EventArgs e)
        {
            chkAttAll.Top = gridAttributes.Top + 10;
        }

        private void gridEntities_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.ColumnIndex == 0)
            {
                DataGridView dgv = sender as DataGridView;
                var data = dgv.Rows[e.RowIndex].DataBoundItem as EntityMetadataProxy;
                if (!string.IsNullOrEmpty(data.Metadata.EntityColor))
                {
                    e.CellStyle.BackColor = ColorTranslator.FromHtml(data.Metadata.EntityColor);
                }
            }
        }

        private void gridEntities_Move(object sender, EventArgs e)
        {
            chkEntAll.Top = gridEntities.Top + 10;
        }

        private void gridEntities_SelectionChanged(object sender, EventArgs e)
        {
            var newselectedEntity = GetSelectedEntity();
            if (newselectedEntity != null && newselectedEntity != selectedEntity)
            {
                selectedEntity = newselectedEntity;
                DisplayAttributes(selectedEntity);
            }
        }

        private void LCG_ConnectionUpdated(object sender, ConnectionUpdatedEventArgs e)
        {
            LogInfo("Connection has changed to: {0}", e.ConnectionDetail.WebApplicationUrl);
            chkEntAll.Visible = false;
            chkAttAll.Visible = false;
            entities = null;
            selectedEntity = null;
            gridAttributes.DataSource = null;
            gridEntities.DataSource = null;
            SettingsLoad(e.ConnectionDetail?.ConnectionName);
            LoadSolutions();
            Enabled = true;
        }

        private void llGroupBoxExpander_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            GroupBoxToggle(sender as LinkLabel);
        }

        private void tmAttSearch_Tick(object sender, EventArgs e)
        {
            tmAttSearch.Stop();
            FilterAttributes(selectedEntity);
        }

        private void tmEntSearch_Tick(object sender, EventArgs e)
        {
            tmEntSearch.Stop();
            FilterEntities();
        }

        private void tsbClose_Click(object sender, EventArgs e)
        {
            CloseTool();
        }

        private void txtAttSearch_TextChanged(object sender, EventArgs e)
        {
            tmAttSearch.Stop();
            tmAttSearch.Start();
        }

        private void txtEntSearch_TextChanged(object sender, EventArgs e)
        {
            tmEntSearch.Stop();
            tmEntSearch.Start();
        }

        #endregion Private Event Handlers

        #region Private Methods

        private void Attribute_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateAttributesStatus();
        }

        private void DisplayAttributes(EntityMetadataProxy entity)
        {
            if (entity != null && entity.Attributes == null)
            {
                LoadAttributes(entity);
            }
            FilterAttributes(entity);
        }

        private void EnableControls(bool enabled)
        {
            UpdateUI(() =>
            {
                Enabled = enabled;
            });
        }

        private void Entity_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateEntitiesStatus();
        }

        private void FilterAttributes(EntityMetadataProxy entity)
        {
            if (entity != null && entity.Attributes != null && entity.Attributes.Count > 0)
            {
                gridAttributes.DataSource = new SortableBindingList<AttributeMetadataProxy>(
                    entity.Attributes
                    .Where(e => (rbAttCustomAll.Checked ||
                         (rbAttCustomTrue.Checked && e.Metadata.IsCustomAttribute.Value) ||
                         (rbAttCustomFalse.Checked && !e.Metadata.IsCustomAttribute.Value)))
                    .Where(e => (rbAttMgdAll.Checked ||
                         (rbAttMgdTrue.Checked && e.Metadata.IsManaged.Value) ||
                         (rbAttMgdFalse.Checked && !e.Metadata.IsManaged.Value)))
                    .Where(e => string.IsNullOrWhiteSpace(txtAttSearch.Text) ||
                        e.Metadata.LogicalName.ToLowerInvariant().Contains(txtAttSearch.Text) ||
                        e.Metadata.DisplayName?.UserLocalizedLabel?.Label?.ToLowerInvariant().Contains(txtAttSearch.Text) == true));
            }
            else
            {
                gridAttributes.DataSource = null;
            }
            UpdateAttributesStatus();
        }

        private void FilterEntities()
        {
            if (entities != null && entities.Count > 0)
            {
                var filteredentities =
                    entities
                    .Where(e => !e.Metadata.IsPrivate.Value)
                    .Where(e => !chkEntSelected.Checked || e.IsSelected)
                    .Where(e => (rbEntCustomAll.Checked ||
                         (rbEntCustomTrue.Checked && e.Metadata.IsCustomEntity.Value) ||
                         (rbEntCustomFalse.Checked && !e.Metadata.IsCustomEntity.Value)))
                    .Where(e => (rbEntMgdAll.Checked ||
                         (rbEntMgdTrue.Checked && e.Metadata.IsManaged.Value) ||
                         (rbEntMgdFalse.Checked && !e.Metadata.IsManaged.Value)))
                    .Where(e => !e.Metadata.IsIntersect.Value || chkEntIntersect.Checked)
                    .Where(e => string.IsNullOrWhiteSpace(txtEntSearch.Text) ||
                        e.Metadata.LogicalName.ToLowerInvariant().Contains(txtEntSearch.Text) ||
                        e.Metadata.DisplayName?.UserLocalizedLabel?.Label?.ToLowerInvariant().Contains(txtEntSearch.Text) == true);
                if (cmbSolution.SelectedItem is SolutionProxy solution)
                {
                    if (solution.Entities == null)
                    {
                        LoadSolutionEntities(solution, FilterEntities);
                        return;
                    }
                    filteredentities = filteredentities
                        .Where(e => solution.Entities.Contains(e.LogicalName));
                }

                gridEntities.DataSource = new SortableBindingList<EntityMetadataProxy>(filteredentities);
            }
            else
            {
                gridEntities.DataSource = null;
            }
            UpdateEntitiesStatus();
        }

        private EntityMetadataProxy GetSelectedEntity()
        {
            if (gridEntities.SelectedRows.Count == 1)
            {
                var row = gridEntities.SelectedRows[0];
                return row.DataBoundItem as EntityMetadataProxy;
            }
            return null;
        }

        private void GropBoxCollapse(LinkLabel link)
        {
            link.Parent.Height = 18;
            link.Text = "Expand";
        }

        private void GroupBoxExpand(LinkLabel link)
        {
            link.Parent.Height = groupBoxHeights[link.Parent.Name];
            link.Text = "Collapse";
        }

        private void GroupBoxToggle(LinkLabel link)
        {
            if (link.Parent.Height > 20)
            {
                GropBoxCollapse(link);
            }
            else
            {
                GroupBoxExpand(link);
            }
        }

        private void LoadAttributes(EntityMetadataProxy entity)
        {
            entity.Attributes = null;
            WorkAsync(new WorkAsyncInfo
            {
                Message = $"Loading attributes for {entity}...",
                Work = (worker, args) =>
                {
                    args.Result = MetadataHelper.LoadEntityDetails(Service, entity.LogicalName);
                },
                PostWorkCallBack = (completedargs) =>
                {
                    if (completedargs.Error != null)
                    {
                        MessageBox.Show(completedargs.Error.Message);
                    }
                    else
                    {
                        if (completedargs.Result is RetrieveMetadataChangesResponse)
                        {
                            var metaresponse = ((RetrieveMetadataChangesResponse)completedargs.Result).EntityMetadata;
                            if (metaresponse.Count == 1)
                            {
                                entity.Attributes = new List<AttributeMetadataProxy>(
                                    metaresponse[0].Attributes
                                    .Select(m => new AttributeMetadataProxy(entity, m))
                                    .OrderBy(a => a.ToString()));
                                foreach (var attribute in entity.Attributes)
                                {
                                    attribute.PropertyChanged += Attribute_PropertyChanged;
                                }
                            }
                        }
                    }
                    UpdateUI(() =>
                    {
                        FilterAttributes(entity);
                        if (gridAttributes.Columns.Count > 0)
                        {
                            gridAttributes.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCellsExceptHeader);
                            gridAttributes.Columns[0].Width = 30;
                        }
                    });
                }
            });
        }

        private void LoadEntities()
        {
            entities = null;
            EnableControls(false);
            gridAttributes.DataSource = null;
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading entities...",
                Work = (worker, args) =>
                {
                    args.Result = MetadataHelper.LoadEntities(Service, ConnectionDetail.OrganizationMajorVersion);
                },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.Message);
                    }
                    else
                    {
                        if (args.Result is RetrieveMetadataChangesResponse)
                        {
                            var metaresponse = ((RetrieveMetadataChangesResponse)args.Result).EntityMetadata;
                            entities = new List<EntityMetadataProxy>(
                                metaresponse
                                .Select(m => new EntityMetadataProxy(m))
                                .OrderBy(e => e.ToString()));
                            foreach (var entity in entities)
                            {
                                entity.PropertyChanged += Entity_PropertyChanged;
                            }
                        }
                    }
                    UpdateUI(() =>
                    {
                        FilterEntities();
                        if (gridEntities.Columns.Count > 0)
                        {
                            gridEntities.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCellsExceptHeader);
                            gridEntities.Columns[0].Width = 30;
                        }
                        EnableControls(true);
                    });
                }
            });
        }

        private void LoadSolutionEntities(SolutionProxy solution, Action callback)
        {
            WorkAsync(new WorkAsyncInfo
            {
                Message = "Loading solution entities...",
                Work = (worker, args) =>
                  {
                      var qx = new QueryExpression("solutioncomponent");
                      qx.ColumnSet.AddColumns("objectid");
                      qx.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 1);
                      qx.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solution.Solution.Id);
                      args.Result = Service.RetrieveMultiple(qx);
                  },
                PostWorkCallBack = (args) =>
                {
                    if (args.Error != null)
                    {
                        MessageBox.Show(args.Error.Message);
                    }
                    if (args.Result is EntityCollection solutionentities)
                    {
                        solution.Entities = entities
                            .Where(e => solutionentities.Entities
                                .Select(i => i["objectid"]).Contains(e.Metadata.MetadataId))
                            .Select(e => e.LogicalName)
                            .ToList();
                        callback?.Invoke();
                    }
                }
            });
        }

        private void LoadSolutions()
        {
            cmbSolution.Items.Clear();
            WorkAsync(new WorkAsyncInfo("Loading solutions...",
                (eventargs) =>
                {
                    EnableControls(false);
                    var qx = new QueryExpression("solution");
                    qx.ColumnSet.AddColumns("friendlyname", "uniquename");
                    //qx.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
                    qx.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);
                    var lePub = qx.AddLink("publisher", "publisherid", "publisherid");
                    lePub.EntityAlias = "P";
                    lePub.Columns.AddColumns("customizationprefix");
                    eventargs.Result = Service.RetrieveMultiple(qx);
                })
            {
                PostWorkCallBack = (completedargs) =>
                {
                    if (completedargs.Error != null)
                    {
                        MessageBox.Show(completedargs.Error.Message);
                    }
                    else
                    {
                        if (completedargs.Result is EntityCollection)
                        {
                            var solutions = (EntityCollection)completedargs.Result;
                            var proxiedsolutions = solutions.Entities.Select(s => new SolutionProxy(s)).OrderBy(s => s.ToString());
                            cmbSolution.Items.Add("");
                            cmbSolution.Items.AddRange(proxiedsolutions.ToArray());
                        }
                    }
                    EnableControls(true);
                }
            });
        }

        private void SetNamespace()
        {
            if (cmbSolution.SelectedItem is SolutionProxy solution && string.IsNullOrWhiteSpace(txtNamespace.Text))
            {
                txtNamespace.Text = solution.UniqueName;
            }
        }

        private void SettingsLoad(string connectionname)
        {
            if (SettingsManager.Instance.TryLoad(GetType(), out Settings settings, connectionname))
            {
                rbEntCustomAll.Checked = settings.EntitiesCustomAll;
                rbEntCustomFalse.Checked = settings.EntitiesCustomFalse;
                rbEntCustomTrue.Checked = settings.EntitiesCustomTrue;
                rbEntMgdAll.Checked = settings.EntitiesManagedAll;
                rbEntMgdTrue.Checked = settings.EntitiesManagedTrue;
                rbEntMgdFalse.Checked = settings.EntitiesManagedFalse;
                chkEntIntersect.Checked = settings.EntitiesIntersect;
                rbAttCustomAll.Checked = settings.AttributesCustomAll;
                rbAttCustomFalse.Checked = settings.AttributesCustomFalse;
                rbAttCustomTrue.Checked = settings.AttributesCustomTrue;
                rbAttMgdAll.Checked = settings.AttributesManagedAll;
                rbAttMgdTrue.Checked = settings.AttributesManagedTrue;
                rbAttMgdFalse.Checked = settings.AttributesManagedFalse;
                if (string.IsNullOrEmpty(txtOutputFolder.Text))
                {
                    txtOutputFolder.Text = settings.OutputFolder;
                }
                if (settings.OptionsExpanded)
                {
                    GroupBoxExpand(llOptionsExpander);
                }
                else
                {
                    GropBoxCollapse(llOptionsExpander);
                }
                if (settings.EntityFilterExpanded)
                {
                    GroupBoxExpand(llEntityExpander);
                }
                else
                {
                    GropBoxCollapse(llEntityExpander);
                }
                if (settings.AttributeFilterExpanded)
                {
                    GroupBoxExpand(llAttributeExpander);
                }
                else
                {
                    GropBoxCollapse(llAttributeExpander);
                }
            }
        }

        private void SettingsSave(string connectionname)
        {
            SettingsManager.Instance.Save(GetType(), new Settings
            {
                EntitiesCustomAll = rbEntCustomAll.Checked,
                EntitiesCustomFalse = rbEntCustomFalse.Checked,
                EntitiesCustomTrue = rbEntCustomTrue.Checked,
                EntitiesManagedAll = rbEntMgdAll.Checked,
                EntitiesManagedTrue = rbEntMgdTrue.Checked,
                EntitiesManagedFalse = rbEntMgdFalse.Checked,
                EntitiesIntersect = chkEntIntersect.Checked,
                AttributesCustomAll = rbAttCustomAll.Checked,
                AttributesCustomFalse = rbAttCustomFalse.Checked,
                AttributesCustomTrue = rbAttCustomTrue.Checked,
                AttributesManagedAll = rbAttMgdAll.Checked,
                AttributesManagedTrue = rbAttMgdTrue.Checked,
                AttributesManagedFalse = rbAttMgdFalse.Checked,
                OutputFolder = txtOutputFolder.Text,
                OptionsExpanded = gbOptions.Height > 20,
                EntityFilterExpanded = gbEntities.Height > 20,
                AttributeFilterExpanded = gbAttributes.Height > 20
            }, connectionname);
        }

        private void UpdateAttributesStatus()
        {
            chkAttAll.Visible = gridAttributes.Rows.Count > 0;
            if (gridAttributes.DataSource != null && selectedEntity != null && selectedEntity.Attributes != null)
            {
                statusAttributesShowing.Text = $"Showing {gridAttributes.Rows.Count} of {selectedEntity.Attributes.Count} attributes.";
                statusAttributesSelected.Text = $"{selectedEntity?.Attributes?.Count(att => att.Selected)} selected.";
            }
            else
            {
                statusAttributesShowing.Text = "No attributes available";
                statusAttributesSelected.Text = "";
            }
        }

        private void UpdateEntitiesStatus()
        {
            chkEntAll.Visible = gridEntities.Rows.Count > 0;
            btnGenerate.Enabled = entities != null && (bool)entities?.Any(e => e.IsSelected);
            if (gridEntities.DataSource != null && entities != null)
            {
                statusEntitiesShowing.Text = $"Showing {gridEntities.Rows.Count} of {entities.Count} entities.";
                statusEntitiesSelected.Text = $"{entities.Count(ent => ent.Selected)} selected.";
            }
            else
            {
                statusEntitiesShowing.Text = "No entities available";
                statusEntitiesSelected.Text = "";
            }
        }

        private void UpdateUI(Action action)
        {
            MethodInvoker mi = delegate
            {
                action();
            };
            if (InvokeRequired)
            {
                //                if (!Disposing)
                {
                    Invoke(mi);
                }
            }
            else
            {
                mi();
            }
        }

        #endregion Private Methods
    }
}