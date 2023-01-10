using System;
using System.IO;
using System.Text.Json;
using System.Xml;

class Program
{
  static readonly HttpClient client = new HttpClient();

  public static async Task Main(String[] args)
  {
    Console.Clear();
    feedList rssList = new feedList();
    List<procesedFeed> podcast = new List<procesedFeed>();
    rawFeed feed = new rawFeed();
    foreach (Uri uri in rssList.urlList)
    {
      Console.Write(uri);
      try
      {
        Console.Write(" ");
        await feed.download(client, uri);
      }
      catch (TaskCanceledException)
      {
        Console.WriteLine("[failed]");
        continue;
      }
      procesedFeed currentPodcast = new procesedFeed(feed.getTitle());
      currentPodcast.addEpisode(feed.getEpisode());
      feed.cleanup();
      podcast.Add(currentPodcast);
      Console.WriteLine("[{0}]", currentPodcast.title);
    }
  }
}

class feedList
{
  public List<Uri> urlList;
  const string FEED_FILENAME = "feedlist.json";

  public feedList()
  {
    if ( File.Exists(FEED_FILENAME) )
    {
      string jsonString = File.ReadAllText(FEED_FILENAME);
      urlList = JsonSerializer.Deserialize<List<Uri>>(jsonString)!;
    }
    else
    {
      urlList = new List<Uri>();
      Console.SetCursorPosition(0, 22);
      Console.Write("Chybí seznam podcastů!");
      this.addUrl();
    }
  }

  public void addUrl()
  {
    Uri uriToAdd;
    Console.SetCursorPosition(0, 23);
    Console.Write("Zadejte adresu RSS: ");
    if ( Uri.TryCreate(Console.ReadLine(), UriKind.Absolute, out uriToAdd) )
    {
      this.urlList.Add(uriToAdd);
      string jsonString = JsonSerializer.Serialize(urlList);
      File.WriteAllText(FEED_FILENAME, jsonString);
    }
  }

}

struct Episode
{
  public string title;
  public string description;
  public Uri link;
}

class rawFeed
{
  private Stream response = default!;
  private XmlReader reader;

  public async Task download(HttpClient client, Uri uri)
  {
   this.response = await client.GetStreamAsync(uri);
   this.reader = XmlReader.Create(this.response);
  }

  public string getTitle()
  {
    bool inside = false;
    while (reader.Read())
    {
      if (inside & reader.NodeType == XmlNodeType.Text)
      {
        string title = reader.Value;
        return title;
      }
      if (reader.Depth == 2 & reader.NodeType == XmlNodeType.Element & reader.Name == "title")
      {
        inside = true;
      }
      else if (reader.Depth == 2 & reader.NodeType == XmlNodeType.EndElement & reader.Name == "title")
      {
        inside = false;
      }
    }
      return "NO TITLE";
  }

  public Episode getEpisode()
  {
    Episode episode = default;
    bool insideItem = false;
    bool insideTitle = false;
    bool insideDescription = false;
    bool insideEnclosure = false;

    while (reader.Read())
    {
      if (insideTitle & reader.NodeType == XmlNodeType.Text)
      {
        episode.title = reader.Value;
      }
      else if (insideDescription  & reader.NodeType == XmlNodeType.Text)
      {
        episode.description = reader.Value;
      }
      else if (insideEnclosure & reader.NodeType == XmlNodeType.Attribute & reader.Name == "url")
      {
        Uri.TryCreate(reader.Value, UriKind.Absolute, out episode.link);
        insideEnclosure = false;
      }

      if (insideItem)
      {
        if (reader.NodeType == XmlNodeType.Element & reader.Name == "title")
        {
          insideTitle = true;
        }
        else if (reader.NodeType == XmlNodeType.EndElement & reader.Name == "title")
        {
        insideTitle = false;
        }

        if (reader.NodeType == XmlNodeType.Element & reader.Name == "description")
        {
          insideDescription = true;
        }
        else if (reader.NodeType == XmlNodeType.EndElement & reader.Name == "description")
        {
          insideDescription = false;
        }

        if (reader.NodeType == XmlNodeType.Element & reader.Name == "enclosure")
        {
          insideEnclosure = true;
        }

      }
      if (reader.Depth == 2)
      {
        if (reader.NodeType == XmlNodeType.Element & reader.Name == "item")
        {
          insideItem = true;
        }
        else if (reader.NodeType == XmlNodeType.EndElement & reader.Name == "item")
        {
          insideItem = false;
          break;
        }
      }
    }
    return episode;
  }

  public void cleanup()
  {
    response.Dispose();
    reader.Dispose();
  }
}

class procesedFeed
{
  public string title;
  public List<Episode> episode;

  public procesedFeed(string title)
  {
    this.title = title;
    this.episode = new List<Episode>();
  }

  public void addEpisode(Episode episode)
  {
    this.episode.Add(episode);
  }
}
