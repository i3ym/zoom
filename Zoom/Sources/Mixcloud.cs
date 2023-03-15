using System.Text;

namespace Zoom.Sources;

public static class Mixcloud
{
    public record SearchResult(string Url, string Author, string Slug, SongInfo Info);

    public static async Task<OperationResult<SearchResult>> SearchUrl(string search)
    {
        var str = new HttpClient().GetStringAsync("https://api.mixcloud.com/search/?type=cloudcast&q=" + search).Result;
        var jsearch = JObject.Parse(str);

        var jsong = jsearch["data"]?.AsJEnumerable().FirstOrDefault();
        var username = jsong?["user"]?["username"]?.Value<string>();
        var slug = jsong?["slug"]?.Value<string>();
        if (jsong is null || username is null || slug is null) return OperationResult.Err("Не удалось найти трек " + search);

        var previewUrl = await TryFetchPreviewUrl(username, slug);
        if (previewUrl is null) return OperationResult.Err("Не удалось найти манифест для " + slug);

        var manifestUrl = "https://audio9.mixcloud.com/secure/dash2/" + previewUrl.Split("previews/")[1].Replace(".mp3", ".m4a") + "/manifest.mpd";
        var post = await new HttpClient().PostAsync("https://www.dlmixcloud.com/ajax.php", new StringContent("url=https://www.mixcloud.com/" + username + "/" + slug + "/", Encoding.UTF8, "application/x-www-form-urlencoded"));
        var json = JObject.Parse(await post.Content.ReadAsStringAsync());

        var author = jsong?["user"]?["name"]?.Value<string>();
        var title = jsong?["name"]?.Value<string>();
        var length = jsong?["audio_length"]?.Value<int>() ?? throw new NullReferenceException();
        var uri = json["url"]?.Value<string>() ?? throw new NullReferenceException();

        SongInfo info;
        if (author is { } && title is { }) info = new SongInfo(author, title, length);
        else info = new SongInfo(null, length);

        return new SearchResult(uri, username, slug, info).AsOpResult();


        static async Task<string?> TryFetchPreviewUrl(string username, string slug)
        {
            #region post

            var post = "{\"query\":\"query CloudcastHeaderQuery(\\n  $lookup: CloudcastLookup!\\n) {\\n  cloudcast: cloudcastLookup(lookup: $lookup) {\\n    id\\n"
                + "    name\\n    picture {\\n      isLight\\n      primaryColor\\n      ...UGCImage_picture\\n    }\\n    owner {\\n      displayName\\n      isViewer\\n"
                + "      isBranded\\n      selectUpsell {\\n        text\\n      }\\n      id\\n    }\\n    repeatPlayAmount\\n    restrictedReason\\n    seekRestriction\\n"
                + "    ...BlurredBackgroundCloudcastContainer_cloudcast\\n    ...HeaderTitle_cloudcast\\n    ...CloudcastRegisterUpsell_cloudcast\\n    ...PlayButton_cloudcast\\n"
                + "    ...HeaderActions_cloudcast\\n    ...HeaderActionsStats_cloudcast\\n    ...Owner_cloudcast\\n    ...CloudcastBaseAutoPlayComponent_cloudcast\\n"
                + "    ...HeaderWaveform_cloudcast\\n    ...SelectUpsellCloudcastContainer_cloudcast\\n    ...RepeatPlayUpsellBar_cloudcast\\n  }\\n  viewer {\\n    me {\\n"
                + "      id\\n    }\\n    restrictedPlayer: featureIsActive(switch: \\\"restricted_player\\\")\\n    hasRepeatPlayFeature: featureIsActive(switch: \\\"repeat_play\\\")\\n"
                + "    ...CloudcastRegisterUpsell_viewer\\n    ...HeaderActions_viewer\\n    ...HeaderWaveform_viewer\\n    ...FollowButton_viewer\\n    ...Owner_viewer\\n"
                + "    id\\n  }\\n}\\n\\nfragment AddToButton_cloudcast on Cloudcast {\\n  id\\n  isUnlisted\\n  isPublic\\n}\\n\\nfragment BlurredBackgroundCloudcastContainer_cloudcast on Cloudcast {\\n"
                + "  picture {\\n    urlRoot\\n    primaryColor\\n  }\\n}\\n\\nfragment CloudcastBaseAutoPlayComponent_cloudcast on Cloudcast {\\n  id\\n  streamInfo {\\n"
                + "    uuid\\n    url\\n    hlsUrl\\n    dashUrl\\n  }\\n  audioLength\\n  seekRestriction\\n  currentPosition\\n}\\n\\nfragment CloudcastRegisterUpsell_cloudcast on Cloudcast {\\n"
                + "  owner {\\n    id\\n    displayName\\n    followers {\\n      totalCount\\n    }\\n  }\\n}\\n\\nfragment CloudcastRegisterUpsell_viewer on Viewer {\\n  me {\\n"
                + "    id\\n  }\\n}\\n\\nfragment ExclusiveCloudcastBadgeContainer_cloudcast on Cloudcast {\\n  isExclusive\\n  isExclusivePreviewOnly\\n  slug\\n  id\\n  owner {\\n"
                + "    username\\n    id\\n  }\\n}\\n\\nfragment FavoriteButton_cloudcast on Cloudcast {\\n  id\\n  isFavorited\\n  isPublic\\n  hiddenStats\\n  favorites {\\n"
                + "    totalCount\\n  }\\n  slug\\n  owner {\\n    id\\n    isFollowing\\n    username\\n    isSelect\\n    displayName\\n    isViewer\\n  }\\n}\\n\\nfragment FavoriteButton_viewer on Viewer {\\n"
                + "  me {\\n    id\\n  }\\n}\\n\\nfragment FollowButton_user on User {\\n  id\\n  isFollowed\\n  isFollowing\\n  isViewer\\n  followers {\\n    totalCount\\n  }\\n"
                + "  username\\n  displayName\\n}\\n\\nfragment FollowButton_viewer on Viewer {\\n  me {\\n    id\\n  }\\n}\\n\\nfragment HeaderActionsStats_cloudcast on Cloudcast {\\n"
                + "  slug\\n  plays\\n  publishDate\\n  hiddenStats\\n  owner {\\n    username\\n    id\\n  }\\n  ...StaffStats_cloudcast\\n}\\n\\nfragment HeaderActions_cloudcast on Cloudcast {\\n"
                + "  owner {\\n    isViewer\\n    isSubscribedTo\\n    username\\n    hasProFeatures\\n    isBranded\\n    id\\n  }\\n  sections {\\n    __typename\\n    ... on Node {\\n"
                + "      __isNode: __typename\\n      id\\n    }\\n  }\\n  id\\n  slug\\n  isExclusive\\n  isUnlisted\\n  isShortLength\\n  ...FavoriteButton_cloudcast\\n  ...AddToButton_cloudcast\\n"
                + "  ...RepostButton_cloudcast\\n  ...ShareCloudcastButton_cloudcast\\n  ...MoreMenu_cloudcast\\n}\\n\\nfragment HeaderActions_viewer on Viewer {\\n  ...FavoriteButton_viewer\\n"
                + "  ...RepostButton_viewer\\n  ...MoreMenu_viewer\\n}\\n\\nfragment HeaderTitle_cloudcast on Cloudcast {\\n  id\\n  name\\n  slug\\n  owner {\\n    username\\n"
                + "    id\\n  }\\n}\\n\\nfragment HeaderWaveform_cloudcast on Cloudcast {\\n  id\\n  waveformUrl\\n  previewUrl\\n  audioLength\\n  isPlayable\\n  streamInfo {\\n"
                + "    hlsUrl\\n    dashUrl\\n    url\\n    uuid\\n  }\\n  restrictedReason\\n  seekRestriction\\n  currentPosition\\n  ...SeekWarning_cloudcast\\n}\\n\\nfragment HeaderWaveform_viewer on Viewer {\\n"
                + "  restrictedPlayer: featureIsActive(switch: \\\"restricted_player\\\")\\n}\\n\\nfragment Hovercard_user on User {\\n  id\\n}\\n\\nfragment MoreMenu_cloudcast on Cloudcast {\\n"
                + "  id\\n  slug\\n  isSpam\\n  owner {\\n    username\\n    isViewer\\n    id\\n  }\\n}\\n\\nfragment MoreMenu_viewer on Viewer {\\n  me {\\n"
                + "    id\\n  }\\n}\\n\\nfragment Owner_cloudcast on Cloudcast {\\n  isExclusive\\n  owner {\\n    id\\n    username\\n    displayName\\n    ...Hovercard_user\\n    ...UserBadge_user\\n"
                + "    ...FollowButton_user\\n  }\\n  ...ExclusiveCloudcastBadgeContainer_cloudcast\\n}\\n\\nfragment Owner_viewer on Viewer {\\n  ...FollowButton_viewer\\n}\\n\\nfragment PlayButton_cloudcast on Cloudcast {\\n"
                + "  restrictedReason\\n  owner {\\n    displayName\\n    country\\n    username\\n    isSubscribedTo\\n    isViewer\\n    id\\n  }\\n  slug\\n  id\\n  isAwaitingAudio\\n  isDraft\\n"
                + "  isPlayable\\n  streamInfo {\\n    hlsUrl\\n    dashUrl\\n    url\\n    uuid\\n  }\\n  audioLength\\n  currentPosition\\n  proportionListened\\n  repeatPlayAmount\\n  hasPlayCompleted\\n"
                + "  seekRestriction\\n  previewUrl\\n  isExclusivePreviewOnly\\n  isExclusive\\n  picture {\\n    primaryColor\\n    isLight\\n    lightPrimaryColor: primaryColor(lighten: 15)\\n"
                + "    transparentPrimaryColor: primaryColor(alpha: 0.3)\\n  }\\n}\\n\\nfragment RepeatPlayUpsellBar_cloudcast on Cloudcast {\\n  owner {\\n    username\\n    displayName\\n    isSelect\\n"
                + "    id\\n  }\\n}\\n\\nfragment RepostButton_cloudcast on Cloudcast {\\n  id\\n  isReposted\\n  isPublic\\n  hiddenStats\\n  reposts {\\n    totalCount\\n  }\\n  owner {\\n    isViewer\\n"
                + "    id\\n  }\\n}\\n\\nfragment RepostButton_viewer on Viewer {\\n  me {\\n    id\\n  }\\n}\\n\\nfragment SeekWarning_cloudcast on Cloudcast {\\n  owner {\\n    displayName\\n    isSelect\\n"
                + "    username\\n    id\\n  }\\n  seekRestriction\\n}\\n\\nfragment SelectUpsellCloudcastContainer_cloudcast on Cloudcast {\\n  __typename\\n  isExclusivePreviewOnly\\n  isExclusive\\n  owner {\\n"
                + "    isSelect\\n    isSubscribedTo\\n    username\\n    displayName\\n    isViewer\\n    id\\n  }\\n}\\n\\nfragment ShareCloudcastButton_cloudcast on Cloudcast {\\n  id\\n  isUnlisted\\n  isPublic\\n"
                + "  slug\\n  description\\n  picture {\\n    urlRoot\\n  }\\n  owner {\\n    displayName\\n    isViewer\\n    username\\n    id\\n  }\\n}\\n\\nfragment StaffStats_cloudcast on Cloudcast {\\n  qualityScore\\n"
                + "  listenerMinutes\\n}\\n\\nfragment UGCImage_picture on Picture {\\n  urlRoot\\n  primaryColor\\n}\\n\\nfragment UserBadge_user on User {\\n  username\\n  hasProFeatures\\n  hasPremiumFeatures\\n  isStaff\\n"
                + "  isSelect\\n}\\n\",\"variables\":{\"lookup\":{\"username\":\"" + username + "\",\"slug\":\"" + slug + "\"}}}";

            #endregion

            var req = new HttpRequestMessage(HttpMethod.Post, "https://www.mixcloud.com/graphql") { Content = new StringContent(post) { Headers = { ContentType = new("application/json") } } };
            req.Headers.Add("Cookie", ZoomConfig.Instance.Get<string>("mixcloudcache"));

            var response = await new HttpClient().SendAsync(req);
            var respstr = await new StreamReader(await response.Content.ReadAsStreamAsync()).ReadToEndAsync();
            return JObject.Parse(respstr)["data"]?["cloudcast"]?["previewUrl"]?.Value<string>();
        }
    }
}
