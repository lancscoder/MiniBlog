﻿using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using System.Xml.Linq;
using System.Xml.XPath;

public static class Storage
{
    private static string _folder = HostingEnvironment.MapPath("~/posts/");

    public static List<Post> GetAllPosts()
    {
        if (HttpRuntime.Cache["posts"] == null)
        {
            if (Settings.UseBlobStorage)
            {
                LoadPostsFromBlob();
            }
            else
            {
                LoadPostsFromDisk();
            }
        }

        if (HttpRuntime.Cache["posts"] != null)
        {
            return (List<Post>)HttpRuntime.Cache["posts"];
        }
        return new List<Post>();
    }

    public static void Save(Post post)
    {
        if (Settings.UseBlobStorage)
        {
            SaveToBlobStorage(post);
        }
        else
        {
            SaveToDisk(post);
        }
    }

    public static void SaveToDisk(Post post)
    {
        string fileName = Path.Combine(_folder, post.ID + ".xml");

        var doc = GetPostXml(post);

        if (!File.Exists(fileName)) // New post
        {
            RefreshCache(post);
        }

        doc.Save(fileName);
    }

    public static void SaveToBlobStorage(Post post)
    {
        var name = String.Format("{0}.xml", post.ID);

        var doc = GetPostXml(post);

        RefreshCache(post);

        var storageAccount = CloudStorageAccount.Parse(Settings.BlobStorageConnectionString);

        var blobClient = storageAccount.CreateCloudBlobClient();

        var container = blobClient.GetContainerReference("posts");

        // Create the container if it doesn't already exist.
        container.CreateIfNotExists();

        var blockBlob = container.GetBlockBlobReference(name);

        blockBlob.UploadText(doc.ToString());
    }

    private static XDocument GetPostXml(Post post)
    {
        post.LastModified = DateTime.UtcNow;

        XDocument doc = new XDocument(
                        new XElement("post",
                            new XElement("title", post.Title),
                            new XElement("slug", post.Slug),
                            new XElement("author", post.Author),
                            new XElement("pubDate", post.PubDate.ToString("yyyy-MM-dd HH:mm:ss")),
                            new XElement("lastModified", post.LastModified.ToString("yyyy-MM-dd HH:mm:ss")),
                            new XElement("content", post.Content),
                            new XElement("ispublished", post.IsPublished),
                            new XElement("categories", string.Empty),
                            new XElement("comments", string.Empty)
                        ));

        XElement categories = doc.XPathSelectElement("post/categories");
        foreach (string category in post.Categories)
        {
            categories.Add(new XElement("category", category));
        }

        XElement comments = doc.XPathSelectElement("post/comments");
        foreach (Comment comment in post.Comments)
        {
            comments.Add(
                new XElement("comment",
                    new XElement("author", comment.Author),
                    new XElement("email", comment.Email),
                    new XElement("website", comment.Website),
                    new XElement("ip", comment.Ip),
                    new XElement("userAgent", comment.UserAgent),
                    new XElement("date", comment.PubDate.ToString("yyyy-MM-dd HH:m:ss")),
                    new XElement("content", comment.Content),
                    new XAttribute("isAdmin", comment.IsAdmin),
                    new XAttribute("id", comment.ID)
                ));
        }

        return doc;
    }

    private static void RefreshCache(Post post)
    {
        var posts = GetAllPosts();

        if (posts.Any(p => p.ID == post.ID)) return;

        posts.Insert(0, post);
        posts.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));
        HttpRuntime.Cache.Insert("posts", posts);
    }

    public static void Delete(Post post)
    {
        if (Settings.UseBlobStorage)
        {
            DeleteFromBlob(post);
        }
        else
        {
            DeleteFromDisk(post);
        }
    }

    private static void DeleteFromDisk(Post post)
    {
        string file = Path.Combine(_folder, post.ID + ".xml");
        File.Delete(file);

        RemoveFromCache(post);
    }

    private static void DeleteFromBlob(Post post)
    {
        var name = String.Format("{0}.xml", post.ID);

        var storageAccount = CloudStorageAccount.Parse(Settings.BlobStorageConnectionString);

        var blobClient = storageAccount.CreateCloudBlobClient();

        var container = blobClient.GetContainerReference("posts");

        // Create the container if it doesn't already exist.
        container.CreateIfNotExists();

        var blockBlob = container.GetBlockBlobReference(name);

        blockBlob.DeleteIfExists();

        RemoveFromCache(post);
    }

    private static void RemoveFromCache(Post post)
    {
        var posts = GetAllPosts();
        posts.Remove(post);
    }

    private static void LoadPostsFromDisk()
    {
        if (!Directory.Exists(_folder))
            Directory.CreateDirectory(_folder);

        List<Post> list = new List<Post>();

        foreach (string file in Directory.GetFiles(_folder, "*.xml", SearchOption.TopDirectoryOnly))
        {
            XElement doc = XElement.Load(file);

            var post = GetPostFromXml(doc, file);

            list.Add(post);
        }

        if (list.Count > 0)
        {
            list.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));
            HttpRuntime.Cache.Insert("posts", list);
        }
    }

    private static void LoadPostsFromBlob()
    {
        List<Post> list = new List<Post>();

        var storageAccount = CloudStorageAccount.Parse(Settings.BlobStorageConnectionString);

        var blobClient = storageAccount.CreateCloudBlobClient();

        var container = blobClient.GetContainerReference("posts");

        // Create the container if it doesn't already exist.
        container.CreateIfNotExists();

        foreach (IListBlobItem item in container.ListBlobs(null, true))
        {
            CloudBlockBlob blob = item as CloudBlockBlob;

            if (!blob.Uri.PathAndQuery.EndsWith(".xml"))
            {
                continue;
            }

            var xml = blob.DownloadText();

            xml = xml.Substring(1);

            XElement doc = XElement.Parse(xml);

            var post = GetPostFromXml(doc, blob.Uri.PathAndQuery);

            list.Add(post);
        }

        if (list.Count > 0)
        {
            list.Sort((p1, p2) => p2.PubDate.CompareTo(p1.PubDate));
            HttpRuntime.Cache.Insert("posts", list);
        }
    }

    private static Post GetPostFromXml(XElement doc, string id)
    {
        var post = new Post()
        {
            ID = Path.GetFileNameWithoutExtension(id),
            Title = ReadValue(doc, "title"),
            Author = ReadValue(doc, "author"),
            Content = ReadValue(doc, "content"),
            Slug = ReadValue(doc, "slug").ToLowerInvariant(),
            PubDate = DateTime.Parse(ReadValue(doc, "pubDate")),
            LastModified = DateTime.Parse(ReadValue(doc, "lastModified", DateTime.Now.ToString())),
            IsPublished = bool.Parse(ReadValue(doc, "ispublished", "true")),
        };

        LoadCategories(post, doc);
        LoadComments(post, doc);

        return post;
    }

    private static void LoadCategories(Post post, XElement doc)
    {
        XElement categories = doc.Element("categories");
        if (categories == null)
            return;

        List<string> list = new List<string>();

        foreach (var node in categories.Elements("category"))
        {
            list.Add(node.Value);
        }

        post.Categories = list.ToArray();
    }

    private static void LoadComments(Post post, XElement doc)
    {
        var comments = doc.Element("comments");

        if (comments == null)
            return;

        foreach (var node in comments.Elements("comment"))
        {
            Comment comment = new Comment()
            {
                ID = ReadAttribute(node, "id"),
                Author = ReadValue(node, "author"),
                Email = ReadValue(node, "email"),
                Website = ReadValue(node, "website"),
                Ip = ReadValue(node, "ip"),
                UserAgent = ReadValue(node, "userAgent"),
                IsAdmin = bool.Parse(ReadAttribute(node, "isAdmin", "false")),
                Content = ReadValue(node, "content").Replace("\n", "<br />"),
                PubDate = DateTime.Parse(ReadValue(node, "date", "2000-01-01")),
            };

            post.Comments.Add(comment);
        }
    }

    private static string ReadValue(XElement doc, XName name, string defaultValue = "")
    {
        if (doc.Element(name) != null)
            return doc.Element(name).Value;

        return defaultValue;
    }

    private static string ReadAttribute(XElement element, XName name, string defaultValue = "")
    {
        if (element.Attribute(name) != null)
            return element.Attribute(name).Value;

        return defaultValue;
    }
}