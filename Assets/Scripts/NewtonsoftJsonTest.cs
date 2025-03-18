using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

public class NewtonsoftJsonTest : MonoBehaviour
{
    [System.Serializable]
    public class TestObject
    {
        public string name;
        public int value;
        public List<string> items;
    }

    void Start()
    {
        // Criar um objeto de teste
        TestObject testObj = new TestObject
        {
            name = "Test Object",
            value = 42,
            items = new List<string> { "Item 1", "Item 2", "Item 3" }
        };

        // Serializar para JSON
        string json = JsonConvert.SerializeObject(testObj, Formatting.Indented);
        Debug.Log("Serializado para JSON:\n" + json);

        // Deserializar de volta
        TestObject deserializedObj = JsonConvert.DeserializeObject<TestObject>(json);
        Debug.Log("Deserializado com sucesso: " + deserializedObj.name);

        // Teste concluído com sucesso
        Debug.Log("Teste Newtonsoft.Json concluído com sucesso!");
    }
} 