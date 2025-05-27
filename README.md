# SocrataCache

SocrataCache aims to provide a caching/downloading layer for Socrata Open Data services.
SocrataCache does this by allowing you to define the datasets you want to download once they come available, and optionally provide the columns you want to download as well.
In this way you can automatically download all data you need automatically and re-distribute it to your own services.

## Story behind

I personally consume a lot of [open data](https://opendata.rdw.nl/) from the Dutch vehicle authority (RDW). 
If you're a bit enthusiastic with your projects, the data you're pulling in will quickly build up to a pile of 20+ Gigabyte. 
Since I consume the same data in multiple project, I was looking for a solution that did allow me to not be dependent on a slow download of multiple big uncompressed files. 
SocrataCache makes sure files are downloaded automatically which enables me to re-distribute the compressed files to my own services. In my case compression resulted in a 80% file size reduction!

This piece of software is far from perfect, since it is my first serious greenfield project in .NET. Please feel invited to provide feedback and/or suggestions as you please.

## Configuration

You need to configure SocrataCache to make sure it automatically downloads your files. Please use the example configuration file and variables below and alter it as you need.

### Configuration file

- `baseURL`: The base url/endpoint of the Socrata service ([more about finding the base/endpoint url](https://dev.socrata.com/consumers/getting-started.html)).
- `retentionSize`: The oldest downloads will be deleted to free up space once the total download directory size in Gigabytes exceeds this value
- `retentionDays`: The oldest downloads will be deleted to free up space once the age of files is older than this defined value
- `webhookUrl`: (Optional) URL to send POST notifications when dataset status changes. If not provided, no notifications will be sent. The webhook payload will include the dataset ID, resource ID, status, reference date, creation date, update date, and type.

Note: Retention cleanup is done on job-basis every few minutes. Treat this job as base-effort, as it may occur that the total volume of files exceeds the threshold. I'm working on a better solution.

### Environment variables

- `SOCRATACACHE_CONFIG_FILE` Path to the JSON configuration file
- `SOCRATACACHE_DB_FILE_PATH` Path where the SQLite database file is stored
- `SOCRATACACHE_DOWNLOADS_ROOT_PATH` Path where downloaded files are stored

Within `resources`:

- `resourceId` An user defined ID for identifying a resource
- `socrataId` The Resource ID as defined in Socrata 
- `excludedColumns` Put all columns you don't want to download in this string array
- `include` Only download the columns defined in this array (opposite of excludedColumns)
- `rawInclude` Same as include, but without checking if the column exists in the dataset
- `query` Map of query parameters to pass to the download URL (e.g. $where, $limit)
- `type` The file type to download. Must be one of: csv, json, xml (default: csv)
- `retainLastFile` If set to true, the retention policy will not delete the last remaining file for this resource, even if it exceeds the age or size thresholds (default: false)

## Example configuration 

This is an example configuration which defines the Socrata service base URL, three resources to download (one with custom column definition), and data retention settings. The webhook URL is optional and can be omitted if you don't need status notifications.

### JSON Configuration

```json
{
  "baseUrl": "https://opendata.rdw.nl",
  "retentionSize": 160,
  "retentionDays": 7,
  "webhookUrl": "https://your-webhook-endpoint.com/notifications",
  "resources": [
    {
      "resourceId": "rdw_vehicle_base_registration",
      "socrataId": "m9d7-ebf2",
      "type": "json",
      "retainLastFile": true,
      "include": [
        "merk",
        "handelsbenaming",
        "aantal_cilinders",
        "cilinderinhoud",
        "aantal_deuren",
        "aantal_wielen",
        "massa_ledig_voertuig",
        "massa_rijklaar"
      ],
      "query": {
        "$where": "merk = 'VOLKSWAGEN'",
        "$limit": "1000"
      }
    },
    {
      "resourceId": "rdw_type_approval_base",
      "socrataId": "byxc-wwua",
      "type": "csv",
      "excludedColumns": [
        "unused_column1",
        "unused_column2"
      ]
    },
    {
      "resourceId": "rdw_type_approval_brand",
      "socrataId": "kyri-nuah",
      "type": "xml",
      "rawInclude": [
        "custom_column1",
        "custom_column2"
      ]
    }
  ]
}
```

### Webhook Notifications

If you configure a `webhookUrl`, SocrataCache will send a POST request to that URL whenever a dataset's status changes. The webhook is completely optional - if you don't need status notifications, simply omit the `webhookUrl` from your configuration.

When a dataset's status changes, a POST request will be sent to the configured webhook URL with a JSON payload like this:

```json
{
    "datasetId": "550e8400-e29b-41d4-a716-446655440000",
    "resourceId": "rdw_json_vehicle_base_registration",
    "status": "downloaded",
    "referenceDate": "2024-03-15T10:30:00Z",
    "createdAt": "2024-03-15T10:30:00Z",
    "updatedAt": "2024-03-15T10:30:00Z",
    "type": "json"
}
```

The status field will be one of: pending, downloading, downloaded, obsolete, failed, or deleted.

# Deploying SocrataCache

I recommend running this service in a container. I've included a `compose.yaml` in this repository which functions as an example on how to quickly run SocrataCache. You can run this by using the following command: `docker compose up --build`.

# API

### `/api/datasets`
This endpoint states all datasets and their statuses.