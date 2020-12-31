import json
from graphviz import Digraph

def remove_prefix(prefix, value):   
    if len(prefix) == 0 or len(prefix) == len(value):
        return value

    if not value.startswith(prefix):
        raise "{value} does not start with {prefix}".format(value, prefix)

    suffix = value[len(prefix):]

    if len(suffix) == 0:
        print(repr(prefix))
        print(repr(value))
        raise 'oops!'

    return "..." + suffix

def get_package_id(value):
    return value.split('$')[-1]

def get_package_url(value, version=None):
    id = get_package_id(value)
    url = "https://www.nuget.org/packages/{id}".format(id=id)
    if version:
        url += "/" + version
    return url

bfs_data = json.load(open('steps-example1-BFS.json', 'rb'))
dfs_data = json.load(open('steps-example1-DFS.json', 'rb'))
is_nuget = False

dfs = Digraph(comment='DFS', filename='dfs', format='svg')
dfs.attr('graph', rankdir='LR')

id_to_node = {node['Id']: node for node in dfs_data}
id_to_children = {}
for node in dfs_data:
    if node['ParentId']:
        if node['ParentId'] not in id_to_children:
            id_to_children[node['ParentId']] = []
        id_to_children[node['ParentId']].append(node)

query_count = 0
for step in dfs_data:

    if step['ParentId']:
        prefix_parent = id_to_node[step['ParentId']]
        while 'PartitionKeyPrefix' not in prefix_parent['Data']:
            prefix_parent = id_to_node[prefix_parent['ParentId']]
        prefix = prefix_parent['Data']['PartitionKeyPrefix']
    else:
        prefix = ''

    if step['Data']['Type'] == 'Start':
        if not is_nuget:
            continue
        
        dfs.attr('node', shape='box', style='rounded')
        label = '<<b>Start</b>>'

    elif step['Data']['Type'] == 'EntitySegment':
        dfs.attr('node', shape='box', style='filled', fillcolor='darkseagreen1')
        if not is_nuget:
            if step['Data']['Count'] == 1:
                label = "<<b>1 result</b>: {0}, {1}>".format(
                    step['Data']['First']['PartitionKey'],
                    step['Data']['First']['RowKey'],
                    step['Data']['Count']
                )
            else:
                label = "<<b>{0} results</b>: {1}, {2} - {3}, {4}>".format(
                    step['Data']['Count'],
                    step['Data']['First']['PartitionKey'],
                    step['Data']['First']['RowKey'],
                    step['Data']['Last']['PartitionKey'],
                    step['Data']['Last']['RowKey']
                )
        elif (step['Data']['First']['PartitionKey'] == step['Data']['Last']['PartitionKey']):
            label = """<
            <table border="0" cellspacing="0" cellpadding="5">
                <tr>
                    <td href="{url}" title="{id}"><u><font color="blue">{pk}</font></u></td>
                    <td>({count} total)</td>
                </tr>
            </table>
            >""".format(
                id=get_package_id(step['Data']['First']['PartitionKey']),
                url=get_package_url(step['Data']['First']['PartitionKey']),
                pk=remove_prefix(prefix, step['Data']['First']['PartitionKey']),
                count=step['Data']['Count'])
        else:
            label = """<
            <table border="0" cellspacing="0" cellpadding="5">
                <tr>
                    <td title="{first_id}" href="{first_url}"><u><font color="blue">{first_pk}</font></u></td>
                    <td>-</td>
                    <td title="{last_id}" href="{last_url}"><u><font color="blue">{last_pk}</font></u></td>
                    <td> ({count} total)</td>
                </tr>
            </table>
            >""".format(
                first_id=get_package_id(step['Data']['First']['PartitionKey']),
                first_url=get_package_url(step['Data']['First']['PartitionKey'], step['Data']['First']['RowKey']),
                first_pk=remove_prefix(prefix, step['Data']['First']['PartitionKey']),
                last_id=get_package_id(step['Data']['Last']['PartitionKey']),
                last_url=get_package_url(step['Data']['Last']['PartitionKey'], step['Data']['Last']['RowKey']),
                last_pk=remove_prefix(prefix, step['Data']['Last']['PartitionKey']),
                count=step['Data']['Count'])

    elif step['Data']['Type'] == 'PartitionKeyQuery':
        query_count += 1
        dfs.attr('node', shape='box', style='filled', fillcolor='beige')
        if not is_nuget:
            label = "<<b>Query {0}</b>: PK = '{1}' and RK &gt; '{2}'>".format(
                query_count,
                step['Data']['PartitionKey'],
                step['Data']['RowKeySkip'],
            )
        elif step['Id'] in id_to_children:
            label = """<
            <table border="0" cellspacing="0" cellpadding="5">
                <tr>
                    <td title="{id}" href="{url}"><u><font color="blue">{pk}</font></u></td>
                </tr>
            </table>
            >""".format(
                id=get_package_id(step['Data']['PartitionKey']),
                url=get_package_url(step['Data']['PartitionKey']),
                pk=remove_prefix(prefix, step['Data']['PartitionKey']))
        else:
            label = "pk: " + remove_prefix(prefix, step['Data']['PartitionKey'])

    elif step['Data']['Type'] == 'PrefixQuery':
        query_count += 1
        dfs.attr('node', shape='box', style='filled', fillcolor='darkslategray1')
        if not is_nuget:
            if len(step['Data']['PartitionKeyLowerBound']) == 0:
                label = "<<b>Query {0}</b>: PK = '{1}*'>".format(
                    query_count,
                    step['Data']['PartitionKeyPrefix'])
            else:
                label = "<<b>Query {0}</b>: PK = '{1}*' and PK &gt; '{2}'>".format(
                    query_count,
                    step['Data']['PartitionKeyPrefix'],
                    step['Data']['PartitionKeyLowerBound'])
        else:
            label = remove_prefix(prefix, step['Data']['PartitionKeyPrefix']) + "*"

    else:
        raise 'Unknown node type'

    # dfs.attr('node', rank=str(step['Data']['Depth']))
    id = str(step['Id'])
    dfs.node(id, label=label)
    if step['ParentId'] and step['ParentId'] > 1:
        dfs.edge(str(step['ParentId']), id)

dfs.save(filename="dfs.dot")
dfs.view()