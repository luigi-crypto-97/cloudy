using FriendMap.Mobile.Models;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Media;

namespace FriendMap.Mobile.Pages;

public partial class MainMapPage
{
    private EditableUserProfile? _myProfile;
    private IReadOnlyList<DirectMessageThreadSummary> _messageInbox = Array.Empty<DirectMessageThreadSummary>();
    private DirectMessageThread? _activeDirectMessageThread;
    private UserProfile? _activeDirectMessageProfile;
    private IReadOnlyList<UserSearchResult> _userSearchResults = Array.Empty<UserSearchResult>();
    private CancellationTokenSource? _userSearchCts;
    private bool _isEditProfileBusy;
    private bool _isDirectMessageBusy;
    private DateTimeOffset _socialOverlayLastRefreshUtc = DateTimeOffset.MinValue;

    private async void OnOpenEditProfileClicked(object? sender, EventArgs e)
    {
        await ShowEditProfileOverlayAsync();
    }

    private void OnEditProfileOverlayBackdropTapped(object? sender, TappedEventArgs e)
    {
        _ = HideEditProfileOverlayAsync(animated: true);
    }

    private void OnEditProfileOverlayCloseClicked(object? sender, EventArgs e)
    {
        _ = HideEditProfileOverlayAsync(animated: true);
    }

    private async void OnSaveProfileClicked(object? sender, EventArgs e)
    {
        if (_isEditProfileBusy)
        {
            return;
        }

        try
        {
            _isEditProfileBusy = true;
            var birthYear = int.TryParse(EditBirthYearEntry.Text?.Trim(), out var parsedBirthYear)
                ? (int?)parsedBirthYear
                : null;
            var updated = await _apiClient.UpdateMyProfileAsync(
                EditDisplayNameEntry.Text?.Trim(),
                EditAvatarUrlEntry.Text?.Trim(),
                EditBioEditor.Text?.Trim(),
                birthYear,
                EditGenderPicker.SelectedItem?.ToString(),
                SplitInterests(EditInterestsEditor.Text));

            _myProfile = updated;
            SetEditProfileStatus("Profilo aggiornato.", false);
            await RefreshSocialOverlayAsync();
            await RefreshForCurrentViewportAsync(force: true, centerOnMarkers: false);
        }
        catch (Exception ex)
        {
            SetEditProfileStatus(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isEditProfileBusy = false;
        }
    }

    private async void OnUploadAvatarClicked(object? sender, EventArgs e)
    {
        if (_isEditProfileBusy)
        {
            return;
        }

        try
        {
            var file = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Scegli immagine profilo"
            });

            if (file is null)
            {
                return;
            }

            _isEditProfileBusy = true;
            var updated = await _apiClient.UploadMyAvatarAsync(file);
            _myProfile = updated;
            PopulateEditProfileForm(updated);
            SetEditProfileStatus("Immagine profilo aggiornata.", false);
            await RefreshSocialOverlayAsync();
            await RefreshForCurrentViewportAsync(force: true, centerOnMarkers: false);
        }
        catch (FeatureNotSupportedException)
        {
            SetEditProfileStatus("Selezione foto non supportata su questo device.", true);
        }
        catch (PermissionException)
        {
            SetEditProfileStatus("Permesso foto non concesso.", true);
        }
        catch (Exception ex)
        {
            SetEditProfileStatus(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isEditProfileBusy = false;
        }
    }

    private void OnEditAvatarUrlChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateEditProfileAvatarPreview(e.NewTextValue);
    }

    private void OnSocialUserSearchChanged(object? sender, TextChangedEventArgs e)
    {
        ScheduleUserSearch(e.NewTextValue);
    }

    private async void OnProfileMessageClicked(object? sender, EventArgs e)
    {
        if (_activeProfile is null || !_activeProfile.CanMessageDirectly)
        {
            SetProfileActionMessage("La chat 1:1 è disponibile solo tra amici non bloccati.", true);
            return;
        }

        await ShowDirectMessageOverlayAsync(_activeProfile);
    }

    private async void OnProfileBlockClicked(object? sender, EventArgs e)
    {
        if (_activeProfile is null || _activeProfile.UserId == Guid.Empty || _isProfileActionBusy)
        {
            return;
        }

        try
        {
            _isProfileActionBusy = true;
            ProfileActionMessageLabel.IsVisible = false;

            if (_activeProfile.IsBlockedByViewer)
            {
                await _apiClient.UnblockUserAsync(_activeProfile.UserId);
                await RefreshActiveProfileAsync("Utente sbloccato.", false);
            }
            else
            {
                await _apiClient.BlockUserAsync(_activeProfile.UserId);
                await HideDirectMessageOverlayAsync(animated: false);
                await RefreshActiveProfileAsync("Utente bloccato.", false);
            }

            await RefreshSocialOverlayAsync();
        }
        catch (Exception ex)
        {
            SetProfileActionMessage(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isProfileActionBusy = false;
            UpdateProfileActionState(_activeProfile, _activeProfilePreview);
        }
    }

    private async void OnProfileReportClicked(object? sender, EventArgs e)
    {
        if (_activeProfile is null || _isProfileActionBusy)
        {
            return;
        }

        try
        {
            _isProfileActionBusy = true;
            await _apiClient.ReportUserAsync(_activeProfile.UserId, "user_report", $"Segnalazione inviata da profile sheet per @{_activeProfile.Nickname}");
            SetProfileActionMessage("Segnalazione inviata.", false);
        }
        catch (Exception ex)
        {
            SetProfileActionMessage(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isProfileActionBusy = false;
        }
    }

    private void OnDirectMessageOverlayBackdropTapped(object? sender, TappedEventArgs e)
    {
        _ = HideDirectMessageOverlayAsync(animated: true);
    }

    private void OnDirectMessageOverlayCloseClicked(object? sender, EventArgs e)
    {
        _ = HideDirectMessageOverlayAsync(animated: true);
    }

    private async void OnSendDirectMessageClicked(object? sender, EventArgs e)
    {
        if (_activeDirectMessageProfile is null || _isDirectMessageBusy)
        {
            return;
        }

        var message = DirectMessageEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            SetDirectMessageStatus("Scrivi un messaggio.", true);
            return;
        }

        try
        {
            _isDirectMessageBusy = true;
            await _apiClient.SendDirectMessageAsync(_activeDirectMessageProfile.UserId, message);
            DirectMessageEntry.Text = string.Empty;
            SetDirectMessageStatus("Messaggio inviato.", false);
            await RefreshDirectMessageThreadAsync();
            await RefreshSocialOverlayAsync();
        }
        catch (Exception ex)
        {
            SetDirectMessageStatus(_apiClient.DescribeException(ex), true);
        }
        finally
        {
            _isDirectMessageBusy = false;
        }
    }

    private async Task ShowEditProfileOverlayAsync()
    {
        await HideDirectMessageOverlayAsync(animated: false);
        await HideProfileOverlayAsync(animated: false);

        EditProfileOverlay.IsVisible = true;
        EditProfileSheet.TranslationY = 500;
        await EditProfileSheet.TranslateTo(0, 0, 220, Easing.CubicOut);

        try
        {
            _myProfile = await _apiClient.GetMyProfileAsync();
            PopulateEditProfileForm(_myProfile);
            SetEditProfileStatus(string.Empty, false);
        }
        catch (Exception ex)
        {
            SetEditProfileStatus(_apiClient.DescribeException(ex), true);
        }
    }

    private async Task HideEditProfileOverlayAsync(bool animated)
    {
        if (!EditProfileOverlay.IsVisible)
        {
            return;
        }

        if (animated)
        {
            await EditProfileSheet.TranslateTo(0, 500, 180, Easing.CubicIn);
        }
        else
        {
            EditProfileSheet.TranslationY = 500;
        }

        EditProfileOverlay.IsVisible = false;
    }

    private void PopulateEditProfileForm(EditableUserProfile profile)
    {
        EditProfileHandleLabel.Text = $"@{profile.Nickname}";
        EditDisplayNameEntry.Text = profile.DisplayName;
        EditAvatarUrlEntry.Text = profile.AvatarUrl;
        EditBirthYearEntry.Text = profile.BirthYear?.ToString();
        EditGenderPicker.SelectedItem = profile.Gender;
        EditBioEditor.Text = profile.Bio;
        EditInterestsEditor.Text = string.Join(", ", profile.Interests);
        UpdateEditProfileAvatarPreview(profile.AvatarUrl, profile.Nickname);
    }

    private Task ShowProfileForPreviewAsync(PresencePreview preview)
    {
        return ShowUserProfileAsync(preview);
    }

    private async Task ShowUserProfileAsync(PresencePreview preview)
    {
        await HideQuickActionRailAsync(animated: false);
        await HideDirectMessageOverlayAsync(animated: false);
        await HideVenueDetailOverlayAsync(animated: false);
        _activeProfilePreview = preview;
        _activeProfile = null;
        ProfileOverlay.IsVisible = true;
        ProfileSheet.TranslationY = 460;
        ProfileActionMessageLabel.IsVisible = false;
        ProfileLoadingIndicator.IsVisible = true;
        ProfileLoadingIndicator.IsRunning = true;
        PopulateProfileOverlay(null, preview);
        await ProfileSheet.TranslateTo(0, 0, 220, Easing.CubicOut);

        try
        {
            var profile = await _apiClient.GetUserProfileAsync(preview.UserId);
            _activeProfile = profile;
            PopulateProfileOverlay(profile, preview);
        }
        catch (Exception ex)
        {
            ProfileStatusLabel.Text = _apiClient.DescribeException(ex);
            UpdateProfileActionState(null, preview);
        }
        finally
        {
            ProfileLoadingIndicator.IsVisible = false;
            ProfileLoadingIndicator.IsRunning = false;
        }
    }

    private void PopulateProfileOverlay(UserProfile? profile, PresencePreview fallback)
    {
        var displayName = profile?.DisplayName;
        var nickname = string.IsNullOrWhiteSpace(profile?.Nickname) ? fallback.Nickname : profile!.Nickname;
        var avatarUrl = string.IsNullOrWhiteSpace(profile?.AvatarUrl) ? fallback.AvatarUrl : profile!.AvatarUrl;

        ProfileAvatarHost.Children.Clear();
        ProfileAvatarHost.Children.Add(CreateAvatarBadge(new PresencePreview
        {
            UserId = fallback.UserId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? fallback.DisplayName : displayName,
            Nickname = nickname,
            AvatarUrl = avatarUrl
        }, 72));

        ProfileNameLabel.Text = string.IsNullOrWhiteSpace(displayName) ? fallback.DisplayName : displayName;
        ProfileHandleLabel.Text = $"@{nickname}";
        ProfileStatusLabel.Text = profile?.StatusLabel ?? "Caricamento profilo...";
        ProfileRelationChipLabel.Text = profile?.RelationshipStatus switch
        {
            null => "Profilo",
            "friend" => "Amico",
            "pending_sent" => "In attesa",
            "pending_received" => "Ti ha aggiunto",
            "self" => "Tu",
            _ => "Non amico"
        };
        ProfileFriendsChipLabel.Text = profile is null
            ? "--"
            : profile.MutualFriendsCount > 0
                ? $"{profile.MutualFriendsCount} in comune"
                : $"{profile.FriendsCount} amici";
        ProfileMetaChipLabel.Text = BuildProfileMeta(profile);
        ProfileBioLabel.Text = profile?.Bio ?? string.Empty;
        ProfileBioLabel.IsVisible = !string.IsNullOrWhiteSpace(profile?.Bio);
        ProfileInterestsLabel.Text = profile?.Interests.Count > 0
            ? string.Join(" • ", profile.Interests)
            : string.Empty;
        ProfileInterestsLabel.IsVisible = profile?.Interests.Count > 0;
        ProfileVenueLabel.Text = string.IsNullOrWhiteSpace(profile?.CurrentVenueName)
            ? "Nessuna venue live"
            : profile.CurrentVenueCategory is null
                ? profile.CurrentVenueName
                : $"{profile.CurrentVenueName} • {profile.CurrentVenueCategory}";
        UpdateProfileActionState(profile, fallback);
    }

    private async Task ShowDirectMessageOverlayAsync(UserProfile profile)
    {
        await HideSocialOverlayAsync(animated: false);
        _activeDirectMessageProfile = profile;
        _activeDirectMessageThread = null;
        DirectMessageTitleLabel.Text = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Nickname : profile.DisplayName;
        DirectMessageSubtitleLabel.Text = $"@{profile.Nickname}";
        DirectMessageStatusLabel.IsVisible = false;
        DirectMessageMessagesStack.Children.Clear();
        DirectMessageOverlay.IsVisible = true;
        DirectMessageSheet.TranslationY = 500;
        await DirectMessageSheet.TranslateTo(0, 0, 220, Easing.CubicOut);
        await RefreshDirectMessageThreadAsync();
    }

    private async Task HideDirectMessageOverlayAsync(bool animated)
    {
        if (!DirectMessageOverlay.IsVisible)
        {
            return;
        }

        if (animated)
        {
            await DirectMessageSheet.TranslateTo(0, 500, 180, Easing.CubicIn);
        }
        else
        {
            DirectMessageSheet.TranslationY = 500;
        }

        DirectMessageOverlay.IsVisible = false;
        DirectMessageMessagesStack.Children.Clear();
        DirectMessageEntry.Text = string.Empty;
        _activeDirectMessageThread = null;
        _activeDirectMessageProfile = null;
    }

    private async Task RefreshDirectMessageThreadAsync()
    {
        if (_activeDirectMessageProfile is null)
        {
            return;
        }

        try
        {
            _activeDirectMessageThread = await _apiClient.GetDirectMessageThreadAsync(_activeDirectMessageProfile.UserId);
            PopulateDirectMessageOverlay(_activeDirectMessageThread);
        }
        catch (Exception ex)
        {
            SetDirectMessageStatus(_apiClient.DescribeException(ex), true);
        }
    }

    private void PopulateDirectMessageOverlay(DirectMessageThread thread)
    {
        DirectMessageTitleLabel.Text = string.IsNullOrWhiteSpace(thread.OtherUser.DisplayName) ? thread.OtherUser.Nickname : thread.OtherUser.DisplayName;
        DirectMessageSubtitleLabel.Text = $"@{thread.OtherUser.Nickname}";
        DirectMessageMessagesStack.Children.Clear();

        if (thread.Messages.Count == 0)
        {
            DirectMessageMessagesStack.Children.Add(CreateSocialPlaceholder("Nessun messaggio ancora."));
            return;
        }

        foreach (var message in thread.Messages)
        {
            DirectMessageMessagesStack.Children.Add(CreateDirectMessageBubble(message));
        }
    }

    private void PopulateDirectMessageSection(IReadOnlyList<DirectMessageThreadSummary> inbox)
    {
        SocialMessagesHeaderLabel.Text = $"Messaggi ({inbox.Count})";
        SocialMessagesStack.Children.Clear();

        if (inbox.Count == 0)
        {
            SocialMessagesStack.Children.Add(CreateSocialPlaceholder("Nessuna chat attiva."));
            return;
        }

        foreach (var thread in inbox)
        {
            SocialMessagesStack.Children.Add(CreateDirectMessageThreadCard(thread));
        }
    }

    private View CreateDirectMessageThreadCard(DirectMessageThreadSummary thread)
    {
        var preview = new PresencePreview
        {
            UserId = thread.OtherUserId,
            Nickname = thread.Nickname,
            DisplayName = string.IsNullOrWhiteSpace(thread.DisplayName) ? thread.Nickname : thread.DisplayName!,
            AvatarUrl = thread.AvatarUrl
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };

        grid.Children.Add(CreateAvatarBadge(preview, 42));
        var labels = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(thread.DisplayName) ? thread.Nickname : thread.DisplayName,
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0F172A")
                },
                new Label
                {
                    Text = thread.LastMessagePreview,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#64748B")
                },
                new Label
                {
                    Text = thread.LastMessageAtUtc.ToLocalTime().ToString("ddd HH:mm"),
                    FontSize = 10,
                    TextColor = Color.FromArgb("#94A3B8")
                }
            }
        };

        grid.Children.Add(labels);
        Grid.SetColumn(labels, 1);

        var card = new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = grid
        };

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenDirectMessageFromInboxAsync(thread.OtherUserId))
        });

        return card;
    }

    private async Task OpenDirectMessageFromInboxAsync(Guid otherUserId)
    {
        var profile = await _apiClient.GetUserProfileAsync(otherUserId);
        await ShowDirectMessageOverlayAsync(profile);
    }

    private View CreateDirectMessageBubble(DirectMessageItem message)
    {
        var bubble = new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = message.IsMine ? Color.FromArgb("#2563EB") : Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            MaximumWidthRequest = 260,
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label
                    {
                        Text = message.IsMine ? "Tu" : (string.IsNullOrWhiteSpace(message.DisplayName) ? message.Nickname : message.DisplayName),
                        FontSize = 11,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = message.IsMine ? Colors.White.WithAlpha(0.92f) : Color.FromArgb("#0F172A")
                    },
                    new Label
                    {
                        Text = message.Body,
                        FontSize = 13,
                        TextColor = message.IsMine ? Colors.White : Color.FromArgb("#0F172A")
                    },
                    new Label
                    {
                        Text = message.SentAtUtc.ToLocalTime().ToString("ddd HH:mm"),
                        FontSize = 10,
                        TextColor = message.IsMine ? Colors.White.WithAlpha(0.82f) : Color.FromArgb("#64748B")
                    }
                }
            }
        };

        return new HorizontalStackLayout
        {
            HorizontalOptions = message.IsMine ? LayoutOptions.End : LayoutOptions.Start,
            Children =
            {
                bubble
            }
        };
    }

    private void SetEditProfileStatus(string message, bool isError)
    {
        EditProfileStatusLabel.Text = message;
        EditProfileStatusLabel.TextColor = isError ? Color.FromArgb("#B91C1C") : Color.FromArgb("#1D4ED8");
        EditProfileStatusLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private void SetDirectMessageStatus(string message, bool isError)
    {
        DirectMessageStatusLabel.Text = message;
        DirectMessageStatusLabel.TextColor = isError ? Color.FromArgb("#B91C1C") : Color.FromArgb("#1D4ED8");
        DirectMessageStatusLabel.IsVisible = !string.IsNullOrWhiteSpace(message);
    }

    private void ScheduleUserSearch(string? query)
    {
        CancelUserSearch();
        var normalized = query?.Trim() ?? string.Empty;
        if (normalized.Length < 2)
        {
            _userSearchResults = Array.Empty<UserSearchResult>();
            PopulateUserSearchSection(normalized, _userSearchResults);
            return;
        }

        _userSearchCts = new CancellationTokenSource();
        var token = _userSearchCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(260, token);
                if (token.IsCancellationRequested)
                {
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    SocialUserSearchIndicator.IsVisible = true;
                    SocialUserSearchIndicator.IsRunning = true;

                    try
                    {
                        _userSearchResults = await _apiClient.SearchUsersAsync(normalized);
                        PopulateUserSearchSection(normalized, _userSearchResults);
                    }
                    catch (Exception ex)
                    {
                        SetSocialStatus(_apiClient.DescribeException(ex), true);
                    }
                    finally
                    {
                        SocialUserSearchIndicator.IsVisible = false;
                        SocialUserSearchIndicator.IsRunning = false;
                    }
                });
            }
            catch (TaskCanceledException)
            {
            }
        }, token);
    }

    private void CancelUserSearch()
    {
        if (_userSearchCts is null)
        {
            return;
        }

        _userSearchCts.Cancel();
        _userSearchCts.Dispose();
        _userSearchCts = null;
    }

    private void PopulateUserSearchSection(string? query, IReadOnlyList<UserSearchResult> results)
    {
        SocialUserSearchStack.Children.Clear();
        var hasQuery = !string.IsNullOrWhiteSpace(query) && query.Trim().Length >= 2;
        SocialSearchHeaderLabel.IsVisible = hasQuery;
        SocialSearchHeaderLabel.Text = hasQuery
            ? $"Risultati ricerca ({results.Count})"
            : "Risultati ricerca";

        if (!hasQuery)
        {
            return;
        }

        if (results.Count == 0)
        {
            SocialUserSearchStack.Children.Add(CreateSocialPlaceholder("Nessun utente trovato."));
            return;
        }

        foreach (var result in results)
        {
            SocialUserSearchStack.Children.Add(CreateUserSearchResultCard(result));
        }
    }

    private View CreateUserSearchResultCard(UserSearchResult result)
    {
        var preview = new PresencePreview
        {
            UserId = result.UserId,
            Nickname = result.Nickname,
            DisplayName = string.IsNullOrWhiteSpace(result.DisplayName) ? result.Nickname : result.DisplayName!,
            AvatarUrl = result.AvatarUrl
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };

        grid.Children.Add(CreateAvatarBadge(preview, 44));

        var labels = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = string.IsNullOrWhiteSpace(result.DisplayName) ? result.Nickname : result.DisplayName,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#0F172A")
                },
                new Label
                {
                    Text = $"@{result.Nickname}",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#64748B")
                },
                new Label
                {
                    Text = result.Interests.Count > 0
                        ? string.Join(" • ", result.Interests.Take(3))
                        : "Profilo FriendMap",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#475569")
                }
            }
        };
        grid.Children.Add(labels);
        Grid.SetColumn(labels, 1);

        View action = result.RelationshipStatus switch
        {
            "none" => CreateSocialActionButton("Aggiungi", true, async () =>
            {
                await _apiClient.SendFriendRequestAsync(result.UserId);
                SetSocialStatus("Richiesta di amicizia inviata.", false);
                await RefreshSocialOverlayAsync();
                ScheduleUserSearch(SocialUserSearchEntry.Text);
            }),
            "pending_received" => CreateSocialActionButton("Accetta", true, async () =>
            {
                await _apiClient.AcceptFriendRequestAsync(result.UserId);
                SetSocialStatus("Richiesta accettata.", false);
                await RefreshSocialOverlayAsync();
                ScheduleUserSearch(SocialUserSearchEntry.Text);
            }),
            "pending_sent" => new Border
            {
                Padding = new Thickness(10, 8),
                BackgroundColor = Color.FromArgb("#EAF0F7"),
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Content = new Label
                {
                    Text = "Inviata",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#475569")
                }
            },
            _ => CreateSocialActionButton("Profilo", false, async () => await OpenProfileFromSocialAsync(preview))
        };

        grid.Children.Add(action);
        Grid.SetColumn(action, 2);

        var card = new Border
        {
            Padding = new Thickness(12, 10),
            BackgroundColor = Color.FromArgb("#F8FBFF"),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Content = grid
        };

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OpenProfileFromSocialAsync(preview))
        });

        return card;
    }

    private void UpdateEditProfileAvatarPreview(string? avatarUrl, string? nickname = null)
    {
        var sourceUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        EditProfileAvatarImage.Source = string.IsNullOrWhiteSpace(sourceUrl)
            ? null
            : ImageSource.FromUri(new Uri(sourceUrl, UriKind.Absolute));
        EditProfileAvatarImage.IsVisible = !string.IsNullOrWhiteSpace(sourceUrl);

        var fallbackSeed = !string.IsNullOrWhiteSpace(EditDisplayNameEntry.Text)
            ? EditDisplayNameEntry.Text
            : nickname ?? _myProfile?.Nickname ?? "FM";

        EditProfileAvatarFallbackLabel.Text = BuildAvatarInitials(fallbackSeed);
        EditProfileAvatarFallbackLabel.IsVisible = string.IsNullOrWhiteSpace(sourceUrl);
    }

    private static string BuildAvatarInitials(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "FM";
        }

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        }

        return string.Concat(parts.Take(2).Select(x => char.ToUpperInvariant(x[0])));
    }

    private static List<string> SplitInterests(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}
