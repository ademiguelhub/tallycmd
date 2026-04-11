namespace Tallycmd.Ui.Services;

public class TallycmdApiClient(HttpClient http)
{
    public Task<HttpResponseMessage> PostAsync(string url, HttpContent? content) =>
        http.PostAsync(url, content);

    public Task<HttpResponseMessage> PostAsJsonAsync<T>(string url, T value) =>
        http.PostAsJsonAsync(url, value);
}
